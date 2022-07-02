using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class AnimatorExtractTool
{
    private const float AnimationPositionError = 0.2f;
    private const float AnimationRotationError = 0.1f;
    private const ModelImporterAnimationCompression Compression = ModelImporterAnimationCompression.Optimal;
    private const int DecimalAccuracy = 1000;
    private const float ErrorParam = 0.000f;
    private static string _fbxPath;

    [MenuItem("Tools/AnimatorClip优化")]

    static void OptimalAnimationClip()
    {
        var objs = Selection.objects;

        foreach (var obj in objs)
        {
            OptimizeObject(obj);
        }
    }

    [MenuItem("Tools/AnimatorController优化")]
    static void AndroidSetting()
    {
        var objs = Selection.GetFiltered(typeof(AnimatorController), SelectionMode.DeepAssets);
        EditorUtility.DisplayProgressBar("AnimatorController优化", "", 0);
        int len = 0;
        foreach (var o in objs)
        {
            len++;
            var obj = (AnimatorController) o;
            if (obj.layers.Length == 0)
            {
                continue;
            }

            AnimatorStateMachine asm = obj.layers[0].stateMachine;
            
            foreach (AnimatorStateTransition cas in asm.anyStateTransitions)
            {
                if (cas.destinationState != null && (cas.destinationState.name == "run" ||
                                                     cas.destinationState.name == "idle" ||
                                                     cas.destinationState.name == "showidle"))
                {
                    cas.canTransitionToSelf = false;
                }
            }

            OptimalAnimationByController(AssetDatabase.GetAssetPath(o));

            EditorUtility.DisplayProgressBar("AnimatorController优化", asm.name, len / objs.Length);
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
    }

    public static void OptimalAnimationByController(string str)
    {
        Debug.LogError("OptimalAnimationByController = " + str);
        var obj = AssetDatabase.LoadAssetAtPath<Object>(str); 
        var aniObj = (AnimatorController) obj;

        if (aniObj == null)
            return;
        if (aniObj.layers.Length == 0)
            return;

        foreach (var layer in aniObj.layers)
        {
            var asm = layer.stateMachine;
            foreach (var stateMachine in asm.stateMachines)
            {
                foreach (var state in stateMachine.stateMachine.states)
                {
                    DealState(state);
                }
            }

            foreach (var state in asm.states)
            {
                DealState(state);
            }
        }
    }

    static void DealState(ChildAnimatorState state)
    {
        if (state.state.motion != null)
        {
            var motion = state.state.motion;
            var path = AssetDatabase.GetAssetPath(motion);
            var type1 = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (type1 == typeof(AnimationClip))
                return;
            var name = OptimizeObject(AssetDatabase.LoadAssetAtPath<Object>(path));

            if (name == "")
                return;                

            var aniPath = path.Replace(Path.GetFileName(path), name + ".anim");

            Debug.LogError("aniPath = " + aniPath);

            motion = AssetDatabase.LoadAssetAtPath<Motion>(aniPath);

            if (motion == null)
            {
                Debug.LogError("没有找到动作 -> " + aniPath);
                return;
            }

            state.state.motion = motion;
        }
    }

    public static string OptimizeObject(Object obj)
    {
        var path = AssetDatabase.GetAssetPath(obj);
        if (path != null && Path.GetExtension(path).ToLower() == ".fbx")
        {
            _fbxPath = path;
            OptimizeModleImporter(path);
            return OptimizeAnimationClip(path);
        }

        return "";
    }

    public static string OptimizeAnimationClip(string fbxPath)
    {
        var objs = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        var folderPath = string.Empty;
        var name = "";
        foreach (var o in objs)
        {
            if (o is AnimationClip)
            {
                if (o.name == "__preview__Take 001")
                    continue;
                folderPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(o));
                OptimizeAnimationCurveData(o as AnimationClip, folderPath);
                name = o.name;
            }
        }

        return name;
    }

    public static bool OptimizeAnimationCurveData(AnimationClip clip, string folderPath)
    {
        if (clip == null)
        {
            Debug.LogError("No Clip error" + "  " + _fbxPath);
            return false;
        }

        var curveDatas = AnimationUtility.GetCurveBindings(clip);
        // var curveDatas = AnimationUtility.GetAllCurves(clip, true);
        if (curveDatas == null || curveDatas.Length == 0)
        {
            Debug.LogError("No AnimationClipCurveData error!" + "  " + _fbxPath + "  " +
                           clip.name);
            return false;
        }


        var newClip = new AnimationClip();
        EditorUtility.CopySerialized(clip, newClip);


        newClip.name = clip.name;
        newClip.ClearCurves();

        // var curves = new AnimationCurve[curveDatas.Length];
        // var index = 0;
        foreach (var dt in curveDatas)
        {
            // var nodeName = dt.path.ToLower().Split('/').Last();
            // 进行过滤
            var curve = AnimationUtility.GetEditorCurve(clip, dt);
            // var curve = dt.curve;
            var keys = curve.keys;

            if (CheckScaleCurveCanRemove(dt, ref keys))
            {
                //移除Scale曲线
                continue;
            }
            for (var i = 0; i < keys.Length; i++)
            {
                keys[i].time = Mathf.Round(keys[i].time * DecimalAccuracy) / DecimalAccuracy;
                keys[i].value = Mathf.Round(keys[i].value * DecimalAccuracy) / DecimalAccuracy;
                keys[i].outTangent = Mathf.Round(keys[i].outTangent * DecimalAccuracy) / DecimalAccuracy;
                keys[i].inTangent = Mathf.Round(keys[i].inTangent * DecimalAccuracy) / DecimalAccuracy;
                keys[i].inWeight = Mathf.Round(keys[i].inWeight * DecimalAccuracy) / DecimalAccuracy;
                keys[i].outWeight = Mathf.Round(keys[i].outWeight * DecimalAccuracy) / DecimalAccuracy;
            }
            
            //过滤位移值没有变化的帧动画
            //因为帧信息有初始位置，所有要保留头尾两帧，如果全部删除会出现初始位置为默认值的问题
            // if (IsFilterApproximateKeyFrame(ref keys))
            // {
            //     var newKeys = new Keyframe[2];
            //     newKeys[0] = keys[0];
            //     newKeys[1] = keys[keys.Length - 1];
            //     keys = newKeys;
            // }

            keys = FilterApproximateKeyFrame(ref keys);

            // keys = TestFilterApproximateKeyFrame(ref keys);
            
            curve.keys = keys;
            //设置新数据
            // newClip.SetCurve(dt.path, dt.type, dt.propertyName, curve);
            AnimationUtility.SetEditorCurve(newClip, dt, curve);
            // curves[index] = curve;
            // index++;
        }
        
        Debug.LogFormat("处理{0}完成", clip.name);
        // AnimationUtility.SetEditorCurves(newClip, curveDatas, curves);
        
        AssetDatabase.CreateAsset(newClip, folderPath + "/" + newClip.name + ".anim");
        var name = newClip.name.ToLower();
        if (name.Contains("battleidle") || name.Contains("run"))
        {
            var settings = AnimationUtility.GetAnimationClipSettings(newClip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(newClip, settings);
        }
        
        AssetDatabase.Refresh();
        
        return true;
    }


    /// <summary>
    /// 设置fbx动画导入格式，默认AnimationPositionError = 0.2，AnimationRotationError = 0.1，根据项目调整，值越低优化压缩越少。
    /// </summary>
    /// <param name="fbxPath"></param>
    private static void OptimizeModleImporter(string fbxPath)
    {
        var modelImporter = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (modelImporter != null)
        {
            var isChange = false;
            if (Compression != modelImporter.animationCompression)
            {
                isChange = true;
                modelImporter.animationCompression = Compression;
                modelImporter.animationPositionError = AnimationPositionError;
                modelImporter.animationRotationError = AnimationRotationError;
                modelImporter.resampleCurves = false;
            }
            else
            {
                if (!Mathf.Approximately(modelImporter.animationPositionError, AnimationPositionError))
                {
                    isChange = true;
                    modelImporter.animationPositionError = AnimationPositionError;
                }

                if (!Mathf.Approximately(modelImporter.animationRotationError, AnimationRotationError))
                {
                    isChange = true;
                    modelImporter.animationRotationError = AnimationRotationError;
                }

                if (modelImporter.resampleCurves)
                {
                    isChange = true;
                    modelImporter.resampleCurves = false;
                }
            }

            if (isChange)
            {
                modelImporter.SaveAndReimport();
                AssetDatabase.Refresh();
            }
        }
    }

    private static Keyframe[] TestFilterApproximateKeyFrame(ref Keyframe[] keys)
    {
        List<Keyframe> newKeys = new List<Keyframe>{keys[0]};

        for (int i = 1; i < keys.Length; i++)
        {
            newKeys.Add(keys[i]);
        }

        return newKeys.ToArray();
    }

    private static bool CheckScaleCurveCanRemove(EditorCurveBinding dt, ref Keyframe[] keys)
    {
        if (!dt.propertyName.Contains("Scale"))
            return false;
        if (keys.Length == 0)
            return true;

        var value = keys[0].value;
        for (int i = 1; i < keys.Length; i++)
        {
            if (value - keys[i].value >= 0.001)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 过滤值一样的序列帧
    /// </summary>
    /// <param name="keys"></param>
    /// <returns></returns>
    private static Keyframe[] FilterApproximateKeyFrame(ref Keyframe[] keys)
    {

        // if (keys.Length == 0)
        //     return keys;
        List<Keyframe> newKeys = new List<Keyframe>{keys[0]};
        Keyframe thinkFrame = keys[0];
        List<Keyframe> tempKeys = new List<Keyframe>();
        for (var i = 1; i < keys.Length; i++)
        {
            // var value = Mathf.Abs(thinkFrame.value - keys[i].value) +
            //             Mathf.Abs(thinkFrame.outTangent - keys[i].outTangent) +
            //             Mathf.Abs(thinkFrame.inTangent - keys[i].inTangent);

            if (Mathf.Abs(thinkFrame.outTangent -
                          keys[i].outTangent) > ErrorParam || Mathf.Abs(thinkFrame.inTangent -
                                                                        keys[i].inTangent) > ErrorParam || i == keys.Length - 1)
            {
                if (tempKeys.Count > 0)
                    newKeys.Add(tempKeys[tempKeys.Count - 1]);
                thinkFrame = keys[i];
                newKeys.Add(keys[i]);
                tempKeys.Clear();
            }
            else
            {
                tempKeys.Add(keys[i]);
            }

            // if (Mathf.Abs(thinkFrame.value - keys[i].value) > ErrorParam || Mathf.Abs(thinkFrame.outTangent -
            //                                                                  keys[i].outTangent) > ErrorParam
            //                                                              || Mathf.Abs(thinkFrame.inTangent -
            //                                                                  keys[i].inTangent) > ErrorParam || i == keys.Length - 1)
            // {
            //     thinkFrame = keys[i];
            //     newKeys.Add(keys[i]);
            // }
            // else
            // {
            //     Debug.LogError("time = " + keys[i].time);
            //     Debug.LogError("thinkFrame.outTangent = " + thinkFrame.outTangent);
            //     Debug.LogError("thinkFrame.inTangent = " + thinkFrame.inTangent);
            //     
            //     Debug.LogError("keys[i].outTangent = " + keys[i].outTangent);
            //     Debug.LogError("keys[i].inTangent = " + keys[i].inTangent);
            // }
            // if (value > ErrorParam)
            // {
            //     thinkFrame = keys[i];
            //     newKeys.Add(keys[i]);
            // }
            // if (Mathf.Abs(keys[i].value - keys[i + 1].value) < 0 ||
            //     Mathf.Abs(keys[i].outTangent - keys[i + 1].outTangent) > 0
            //     || Mathf.Abs(keys[i].inTangent - keys[i + 1].inTangent) > 0)
            // {
            //     return false;
            // }
        }
        return newKeys.ToArray();
    }
}