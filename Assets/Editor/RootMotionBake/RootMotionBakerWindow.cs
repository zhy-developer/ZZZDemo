using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class RootMotionBakerWindow : EditorWindow
{
    // ============================================================
    // 输入
    // ============================================================

    /// <summary>
    /// 实际角色 Prefab。
    ///
    /// 使用真实角色，可以保证：
    /// Avatar
    /// 模型层级
    /// 模型缩放
    ///
    /// 与游戏中的角色一致。
    /// </summary>
    private GameObject characterPrefab;


    /// <summary>
    /// 要烘焙的 AnimationClip。
    /// </summary>
    private AnimationClip animationClip;


    // ============================================================
    // 帧同步参数
    // ============================================================

    /// <summary>
    /// 游戏逻辑更新间隔。
    ///
    /// 你的项目：
    /// 每 33ms 更新一次。
    /// </summary>
    private int logicFrameIntervalMs = 33;


    /// <summary>
    /// 浮点位置转整数位置倍率。
    ///
    /// 例如：
    ///
    /// 1 Unity Unit
    /// =
    /// 10000 Logic Unit
    /// </summary>
    private int precision = 10000;


    // ============================================================
    // RootMotion 范围
    // ============================================================

    /// <summary>
    /// RootMotion 在动画第几帧结束。
    ///
    /// -1：
    /// 烘焙整个 AnimationClip。
    ///
    /// 例如：
    ///
    /// 你的闪避动画：
    /// 60FPS
    /// 前45动画帧有位移
    ///
    /// 填：
    /// 45
    ///
    /// 就会转换成：
    ///
    /// 45 / 60
    /// =
    /// 0.75秒
    /// </summary>
    private int rootMotionEndAnimationFrame = -1;


    // ============================================================
    // Window
    // ============================================================

    [MenuItem("Tools/FrameSync/Root Motion JSON Baker")]
    public static void OpenWindow()
    {
        GetWindow<RootMotionBakerWindow>(
            "Root Motion Baker");
    }


    private void OnGUI()
    {
        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField(
            "Root Motion JSON 烘焙工具",
            EditorStyles.boldLabel);

        EditorGUILayout.Space(10);


        // ========================================================
        // 基础配置
        // ========================================================

        characterPrefab =
            (GameObject)EditorGUILayout.ObjectField(
                "角色 Prefab",
                characterPrefab,
                typeof(GameObject),
                true);


        animationClip =
            (AnimationClip)EditorGUILayout.ObjectField(
                "Animation Clip",
                animationClip,
                typeof(AnimationClip),
                false);


        EditorGUILayout.Space(10);


        // ========================================================
        // 逻辑帧配置
        // ========================================================

        EditorGUILayout.LabelField(
            "帧同步配置",
            EditorStyles.boldLabel);


        logicFrameIntervalMs =
            EditorGUILayout.IntField(
                "逻辑帧间隔(ms)",
                logicFrameIntervalMs);


        precision =
            EditorGUILayout.IntField(
                "整数精度",
                precision);


        EditorGUILayout.Space(10);


        // ========================================================
        // RootMotion范围
        // ========================================================

        EditorGUILayout.LabelField(
            "Root Motion 范围",
            EditorStyles.boldLabel);


        rootMotionEndAnimationFrame =
            EditorGUILayout.IntField(
                "结束动画帧(-1=全部)",
                rootMotionEndAnimationFrame);


        EditorGUILayout.Space(10);


        // ========================================================
        // AnimationClip 信息
        // ========================================================

        if (animationClip != null)
        {
            DrawClipInfo();
        }


        EditorGUILayout.Space(15);


        // ========================================================
        // Bake
        // ========================================================

        GUI.enabled =
            characterPrefab != null &&
            animationClip != null;


        if (GUILayout.Button(
                "开始烘焙 Root Motion JSON",
                GUILayout.Height(45)))
        {
            Bake();
        }


        GUI.enabled = true;
    }


    /// <summary>
    /// 显示 AnimationClip 和最终采样信息。
    /// </summary>
    private void DrawClipInfo()
    {
        float animationFrameRate =
            animationClip.frameRate;


        float clipLength =
            animationClip.length;


        int totalAnimationFrames =
            Mathf.RoundToInt(
                clipLength *
                animationFrameRate);


        double rootMotionEndTime =
            GetRootMotionEndTime();


        int expectedLogicFrames =
            Mathf.CeilToInt(
                (float)(
                    rootMotionEndTime /
                    (logicFrameIntervalMs / 1000.0)
                ));


        string endDescription;


        if (rootMotionEndAnimationFrame < 0)
        {
            endDescription =
                "整个 AnimationClip";
        }
        else
        {
            endDescription =
                $"{rootMotionEndAnimationFrame}帧 " +
                $"≈ {rootMotionEndTime:F3}s";
        }


        EditorGUILayout.HelpBox(
            $"Animation FPS：{animationFrameRate}\n" +
            $"Animation Length：{clipLength:F3}s\n" +
            $"Animation Frames：约 {totalAnimationFrames}\n\n" +

            $"逻辑采样间隔：{logicFrameIntervalMs}ms\n" +
            $"RootMotion结束：{endDescription}\n" +
            $"预计生成逻辑帧：{expectedLogicFrames}",
            MessageType.Info);
    }


    // ============================================================
    // Bake
    // ============================================================

    private void Bake()
    {
        if (!ValidateInput())
        {
            return;
        }


        GameObject tempCharacter = null;

        PlayableGraph graph = default;


        try
        {
            // ====================================================
            // 1. 创建临时角色
            // ====================================================

            tempCharacter =
                Instantiate(characterPrefab);


            tempCharacter.name =
                characterPrefab.name +
                "_RootMotionBakeTemp";


            tempCharacter.hideFlags =
                HideFlags.HideAndDontSave;


            /*
             * 临时角色放到世界原点。
             */
            tempCharacter.transform.position =
                Vector3.zero;


            /*
             * 保持实际角色朝向。
             */
            tempCharacter.transform.rotation =
                characterPrefab.transform.rotation;


            /*
             * 如果传入的是场景角色，
             * 它可能受到父节点 Scale 影响。
             *
             * 使用 lossyScale 尽可能保持
             * 最终模型缩放一致。
             */
            tempCharacter.transform.localScale =
                characterPrefab.transform.lossyScale;


            tempCharacter.SetActive(true);


            // ====================================================
            // 2. Animator
            // ====================================================

            Animator animator =
                tempCharacter.GetComponentInChildren
                <Animator>(true);


            if (animator == null)
            {
                Debug.LogError(
                    "角色上没有找到 Animator。");

                return;
            }


            if (animator.avatar == null)
            {
                Debug.LogWarning(
                    "Animator 没有 Avatar。" +
                    "如果这是 Humanoid 动画，请检查 Avatar 配置。");
            }


            // ====================================================
            // 3. 禁止游戏逻辑 / 物理干扰
            // ====================================================

            DisableOtherComponents(
                tempCharacter,
                animator);


            // ====================================================
            // 4. 初始化 Animator
            // ====================================================

            animator.enabled = true;


            /*
             * 不使用 AnimatorController。
             *
             * 我们直接使用 AnimationClipPlayable
             * 驱动动画。
             */
            animator.runtimeAnimatorController =
                null;


            /*
             * 开启 RootMotion，
             * 才能正确读取 animator.deltaPosition。
             */
            animator.applyRootMotion =
                true;


            /*
             * 即使角色不可见，
             * Animator 也继续更新。
             */
            animator.cullingMode =
                AnimatorCullingMode.AlwaysAnimate;


            animator.updateMode =
                AnimatorUpdateMode.Normal;


            animator.Rebind();


            // ====================================================
            // 5. PlayableGraph
            // ====================================================

            graph =
                PlayableGraph.Create(
                    "RootMotionBakeGraph");


            /*
             * 非常关键：
             *
             * 使用 Manual。
             *
             * AnimationClip 是 60FPS
             * 并不意味着这里按照 60FPS 更新。
             *
             * 我们自己决定每次推进多少时间。
             */
            graph.SetTimeUpdateMode(
                DirectorUpdateMode.Manual);


            AnimationClipPlayable clipPlayable =
                AnimationClipPlayable.Create(
                    graph,
                    animationClip);


            clipPlayable.SetApplyFootIK(false);

            clipPlayable.SetApplyPlayableIK(false);

            clipPlayable.SetSpeed(1.0);


            /*
             * 明确从动画起点开始。
             */
            clipPlayable.SetTime(0.0);


            AnimationPlayableOutput output =
                AnimationPlayableOutput.Create(
                    graph,
                    "RootMotionOutput",
                    animator);


            output.SetSourcePlayable(
                clipPlayable);


            graph.Play();


            // ====================================================
            // 6. 初始化动画第0秒
            // ====================================================

            /*
             * Evaluate(0) 不推进动画时间。
             *
             * 它的作用只是：
             *
             * 让 Animator
             * Playable
             * Avatar
             *
             * 全部进入动画0秒的正确状态。
             */
            graph.Evaluate(0f);


            // ====================================================
            // 7. 保存角色初始朝向
            // ====================================================

            Quaternion initialRotation =
                animator.transform.rotation;


            /*
             * 世界空间位移
             * ↓
             * 转换为动画开始时角色的局部坐标。
             */
            Quaternion worldToInitialLocal =
                Quaternion.Inverse(
                    initialRotation);


            // ====================================================
            // 8. 烘焙时间范围
            // ====================================================

            double rootMotionEndTime =
                GetRootMotionEndTime();


            /*
             * 注意：
             *
             * 这里完全按照逻辑时间采样。
             *
             * 33ms
             * =
             * 0.033秒
             *
             * 与 AnimationClip 是
             * 30FPS / 60FPS / 120FPS
             *
             * 没有直接关系。
             */
            double logicDeltaTime =
                logicFrameIntervalMs /
                1000.0;


            // ====================================================
            // 9. JSON对象
            // ====================================================

            RootMotionJsonData jsonData =
                new RootMotionJsonData();


            jsonData.clipName =
                animationClip.name;


            jsonData.sourceAnimationFrameRate =
                Mathf.RoundToInt(
                    animationClip.frameRate);


            jsonData.logicFrameIntervalMs =
                logicFrameIntervalMs;


            jsonData.precision =
                precision;


            jsonData.clipLengthMs =
                Mathf.RoundToInt(
                    animationClip.length *
                    1000f);


            jsonData.rootMotionEndAnimationFrame =
                rootMotionEndAnimationFrame;


            jsonData.rootMotionEndTimeMs =
                Mathf.RoundToInt(
                    (float)(
                        rootMotionEndTime *
                        1000.0
                    ));


            // ====================================================
            // 10. 烘焙变量
            // ====================================================

            /*
             * 使用 double 保存时间，
             * 减少长动画累计的时间误差。
             */
            double currentTime =
                0.0;


            int logicFrameIndex =
                0;


            /*
             * RootMotion 浮点累计位置。
             */
            Vector3 accumulatedPosition =
                Vector3.zero;


            /*
             * 上一逻辑帧整数累计位置。
             */
            Vector3Int previousIntPosition =
                Vector3Int.zero;


            // ====================================================
            // 11. 按逻辑时间逐段采样
            // ====================================================

            while (
                currentTime <
                rootMotionEndTime - 0.0000001)
            {
                /*
                 * 不使用：
                 *
                 * currentTime += 0.033
                 *
                 * 一直累加。
                 *
                 * 而是直接根据：
                 *
                 * logicFrameIndex
                 *
                 * 算出这一帧的目标时间。
                 *
                 * 这样可以进一步减少时间累计误差。
                 */
                double targetTime =
                    Math.Min(
                        (logicFrameIndex + 1) *
                        logicDeltaTime,

                        rootMotionEndTime);


                double sampleDuration =
                    targetTime -
                    currentTime;


                // ================================================
                // 真正推进动画
                // ================================================

                graph.Evaluate(
                    (float)sampleDuration);


                currentTime =
                    targetTime;


                // ================================================
                // 读取 Animator RootMotion
                // ================================================

                Vector3 worldDelta =
                    animator.deltaPosition;


                // ================================================
                // 转换到角色初始朝向局部空间
                // ================================================

                Vector3 localDelta =
                    worldToInitialLocal *
                    worldDelta;


                // ================================================
                // 浮点累计位置
                // ================================================

                accumulatedPosition +=
                    localDelta;


                // ================================================
                // 累计位置整数化
                // ================================================

                Vector3Int currentIntPosition =
                    new Vector3Int(

                        Mathf.RoundToInt(
                            accumulatedPosition.x *
                            precision),

                        Mathf.RoundToInt(
                            accumulatedPosition.y *
                            precision),

                        Mathf.RoundToInt(
                            accumulatedPosition.z *
                            precision)
                    );


                /*
                 * 当前逻辑帧整数位移：
                 *
                 * 当前累计整数位置
                 * -
                 * 上一帧累计整数位置
                 *
                 * 而不是直接：
                 *
                 * Round(localDelta)
                 *
                 * 这样可以明显降低累计舍入误差。
                 */
                Vector3Int deltaInt =
                    currentIntPosition -
                    previousIntPosition;


                // ================================================
                // 保存这一逻辑帧
                // ================================================

                RootMotionJsonFrame frame =
                    new RootMotionJsonFrame();


                frame.logicFrame =
                    logicFrameIndex;


                frame.sampleEndTimeMs =
                    Mathf.RoundToInt(
                        (float)(
                            currentTime *
                            1000.0
                        ));


                frame.sampleDurationMs =
                    Mathf.RoundToInt(
                        (float)(
                            sampleDuration *
                            1000.0
                        ));


                frame.deltaX =
                    deltaInt.x;

                frame.deltaY =
                    deltaInt.y;

                frame.deltaZ =
                    deltaInt.z;


                frame.positionX =
                    currentIntPosition.x;

                frame.positionY =
                    currentIntPosition.y;

                frame.positionZ =
                    currentIntPosition.z;


                jsonData.frames.Add(
                    frame);


                // ================================================
                // 下一逻辑帧
                // ================================================

                previousIntPosition =
                    currentIntPosition;


                logicFrameIndex++;
            }


            // ====================================================
            // 12. 最终数据
            // ====================================================

            jsonData.frameCount =
                jsonData.frames.Count;


            jsonData.totalX =
                previousIntPosition.x;

            jsonData.totalY =
                previousIntPosition.y;

            jsonData.totalZ =
                previousIntPosition.z;


            // ====================================================
            // 13. 生成 JSON
            // ====================================================

            string json =
                JsonUtility.ToJson(
                    jsonData,
                    true);


            // ====================================================
            // 14. 保存
            // ====================================================

            string path =
                EditorUtility.SaveFilePanelInProject(
                    "保存 Root Motion JSON",
                    animationClip.name +
                    "_RootMotion",
                    "json",
                    "请选择 RootMotion JSON 保存位置");


            if (string.IsNullOrEmpty(path))
            {
                Debug.Log(
                    "取消 RootMotion JSON 保存。");

                return;
            }


            /*
             * UTF8，无 BOM。
             */
            File.WriteAllText(
                path,
                json,
                new UTF8Encoding(false));


            AssetDatabase.Refresh();


            TextAsset savedAsset =
                AssetDatabase.LoadAssetAtPath
                <TextAsset>(path);


            if (savedAsset != null)
            {
                Selection.activeObject =
                    savedAsset;
            }


            // ====================================================
            // 15. 输出结果
            // ====================================================

            Debug.Log(
                "========== Root Motion 烘焙完成 ==========\n" +

                $"AnimationClip：{animationClip.name}\n" +

                $"Animation FPS：{animationClip.frameRate}\n" +

                $"逻辑帧间隔：{logicFrameIntervalMs}ms\n" +

                $"RootMotion结束时间：" +
                $"{rootMotionEndTime:F3}s\n" +

                $"逻辑帧数量：" +
                $"{jsonData.frameCount}\n" +

                $"最终整数位移：" +
                $"({jsonData.totalX}, " +
                $"{jsonData.totalY}, " +
                $"{jsonData.totalZ})\n" +

                $"JSON：{path}");


            // ====================================================
            // 16. 如果完全没有位移，给出提示
            // ====================================================

            if (
                previousIntPosition ==
                Vector3Int.zero)
            {
                Debug.LogWarning(
                    "此次烘焙最终位移为 0。\n" +
                    "如果这个动画本来应该有 Root Motion，" +
                    "请检查 Animation Import Settings 中 " +
                    "Root Transform Position (XZ) 是否被 Bake Into Pose。");
            }
        }
        finally
        {
            // ====================================================
            // 清理
            // ====================================================

            if (graph.IsValid())
            {
                graph.Destroy();
            }


            if (tempCharacter != null)
            {
                DestroyImmediate(
                    tempCharacter);
            }
        }
    }


    // ============================================================
    // 时间计算
    // ============================================================

    /// <summary>
    /// 得到 RootMotion 实际结束时间。
    ///
    /// AnimationClip.frameRate
    /// 只在这里参与计算：
    ///
    /// “动画帧”
    /// ↓
    /// “秒”
    ///
    /// 它不参与逻辑采样频率。
    /// </summary>
    private double GetRootMotionEndTime()
    {
        if (animationClip == null)
        {
            return 0.0;
        }


        /*
         * -1：
         * 整个 AnimationClip。
         */
        if (rootMotionEndAnimationFrame < 0)
        {
            return animationClip.length;
        }


        /*
         * 例如：
         *
         * animationFrameRate = 60
         * endFrame = 45
         *
         * 45 / 60
         * =
         * 0.75秒
         */
        double endTime =
            rootMotionEndAnimationFrame /
            (double)animationClip.frameRate;


        /*
         * 不允许超过 AnimationClip 实际长度。
         */
        endTime =
            Math.Min(
                endTime,
                animationClip.length);


        return Math.Max(
            0.0,
            endTime);
    }


    // ============================================================
    // 输入检查
    // ============================================================

    private bool ValidateInput()
    {
        if (characterPrefab == null)
        {
            Debug.LogError(
                "没有指定角色 Prefab。");

            return false;
        }


        if (animationClip == null)
        {
            Debug.LogError(
                "没有指定 AnimationClip。");

            return false;
        }


        if (logicFrameIntervalMs <= 0)
        {
            Debug.LogError(
                "逻辑帧间隔必须 > 0。");

            return false;
        }


        if (precision <= 0)
        {
            Debug.LogError(
                "整数精度必须 > 0。");

            return false;
        }


        if (animationClip.frameRate <= 0)
        {
            Debug.LogError(
                "AnimationClip.frameRate 无效。");

            return false;
        }


        if (rootMotionEndAnimationFrame == 0)
        {
            Debug.LogError(
                "RootMotion结束动画帧不能为0。\n" +
                "如果需要烘焙整个动画，请填写 -1。");

            return false;
        }


        return true;
    }


    // ============================================================
    // 禁止干扰组件
    // ============================================================

    /// <summary>
    /// 禁止可能修改角色 Transform 的逻辑组件和物理组件。
    /// </summary>
    private void DisableOtherComponents(
        GameObject character,
        Animator targetAnimator)
    {
        // ========================================================
        // Behaviour
        //
        // 包括：
        //
        // MonoBehaviour
        // NavMeshAgent 等
        //
        // Animator 本身除外。
        // ========================================================

        Behaviour[] behaviours =
            character.GetComponentsInChildren
            <Behaviour>(true);


        foreach (Behaviour behaviour in behaviours)
        {
            if (behaviour == null)
            {
                continue;
            }


            if (behaviour ==
                targetAnimator)
            {
                continue;
            }


            behaviour.enabled =
                false;
        }


        // ========================================================
        // Rigidbody
        //
        // Unity 2022.3 使用 velocity。
        // ========================================================

        Rigidbody[] rigidbodies =
            character.GetComponentsInChildren
            <Rigidbody>(true);


        foreach (Rigidbody rb in rigidbodies)
        {
            rb.isKinematic =
                true;


            rb.useGravity =
                false;


            rb.detectCollisions =
                false;


            rb.velocity =
                Vector3.zero;


            rb.angularVelocity =
                Vector3.zero;
        }


        // ========================================================
        // Collider
        // ========================================================

        Collider[] colliders =
            character.GetComponentsInChildren
            <Collider>(true);


        foreach (Collider collider in colliders)
        {
            collider.enabled =
                false;
        }


        // ========================================================
        // Rigidbody2D
        // ========================================================

        Rigidbody2D[] rigidbody2Ds =
            character.GetComponentsInChildren
            <Rigidbody2D>(true);


        foreach (Rigidbody2D rb in rigidbody2Ds)
        {
            rb.simulated =
                false;
        }


        // ========================================================
        // Collider2D
        // ========================================================

        Collider2D[] collider2Ds =
            character.GetComponentsInChildren
            <Collider2D>(true);


        foreach (Collider2D collider in collider2Ds)
        {
            collider.enabled =
                false;
        }
    }
}