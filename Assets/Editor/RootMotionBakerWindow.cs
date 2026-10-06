using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class RootMotionBakerWindow : EditorWindow
{
    private GameObject characterPrefab;
    private AnimationClip animationClip;

    // 采样帧率
    private int sampleRate = 30;

    // 例如项目中 1 米 = 10000 整数单位
    private int precision = 10000;


    [MenuItem("编辑器/FrameSync/Root Motion Baker")]
    public static void OpenWindow()
    {
        GetWindow<RootMotionBakerWindow>("Root Motion Baker");
    }


    private void OnGUI()
    {
        EditorGUILayout.Space();

        EditorGUILayout.LabelField(
            "Root Motion 离线烘焙",
            EditorStyles.boldLabel);

        EditorGUILayout.Space();

        characterPrefab = (GameObject)EditorGUILayout.ObjectField(
            "角色 Prefab",
            characterPrefab,
            typeof(GameObject),
            true);

        animationClip = (AnimationClip)EditorGUILayout.ObjectField(
            "Animation Clip",
            animationClip,
            typeof(AnimationClip),
            false);

        sampleRate = EditorGUILayout.IntField(
            "采样帧率",
            sampleRate);

        precision = EditorGUILayout.IntField(
            "整数精度",
            precision);

        EditorGUILayout.Space();

        if (GUILayout.Button(
                "开始烘焙",
                GUILayout.Height(40)))
        {
            Bake();
        }
    }


    private void Bake()
    {
        if (characterPrefab == null)
        {
            Debug.LogError("没有指定角色 Prefab");
            return;
        }

        if (animationClip == null)
        {
            Debug.LogError("没有指定 AnimationClip");
            return;
        }

        if (sampleRate <= 0)
        {
            Debug.LogError("SampleRate 必须 > 0");
            return;
        }

        GameObject tempCharacter = null;
        PlayableGraph graph = default;

        try
        {
            // =========================================================
            // 1. 创建临时角色
            // =========================================================

            tempCharacter = Instantiate(characterPrefab);

            tempCharacter.name =
                characterPrefab.name + "_RootMotionBakeTemp";

            tempCharacter.hideFlags =
                HideFlags.HideAndDontSave;

            /*
             * 如果 characterPrefab 是场景中的对象，
             * 它可能存在父节点缩放。
             *
             * clone 到根节点后，父节点缩放会丢失。
             *
             * 所以这里使用 lossyScale 保证模型最终缩放一致。
             */
            tempCharacter.transform.localScale =
                characterPrefab.transform.lossyScale;

            tempCharacter.transform.position =
                Vector3.zero;

            tempCharacter.transform.rotation =
                characterPrefab.transform.rotation;

            tempCharacter.SetActive(true);


            // =========================================================
            // 2. 找到 Animator
            // =========================================================

            Animator animator =
                tempCharacter.GetComponentInChildren<Animator>(true);

            if (animator == null)
            {
                Debug.LogError("角色上没有 Animator");
                return;
            }


            // =========================================================
            // 3. 禁掉游戏逻辑
            // =========================================================

            DisableOtherComponents(
                tempCharacter,
                animator);


            // =========================================================
            // 4. Animator 初始化
            // =========================================================

            animator.enabled = true;

            /*
             * 不需要原来的 AnimatorController。
             *
             * 动画完全交给 PlayableGraph 驱动。
             */
            animator.runtimeAnimatorController = null;

            /*
             * 必须启用 RootMotion。
             *
             * 临时角色本身移动没关系，
             * 因为我们就是想获得真实 deltaPosition。
             */
            animator.applyRootMotion = true;

            animator.cullingMode =
                AnimatorCullingMode.AlwaysAnimate;

            animator.updateMode =
                AnimatorUpdateMode.Normal;

            animator.Rebind();


            // =========================================================
            // 5. 创建手动 PlayableGraph
            // =========================================================

            graph = PlayableGraph.Create(
                "RootMotionBakeGraph");

            graph.SetTimeUpdateMode(
                DirectorUpdateMode.Manual);


            AnimationClipPlayable clipPlayable =
                AnimationClipPlayable.Create(
                    graph,
                    animationClip);

            clipPlayable.SetApplyFootIK(false);

            clipPlayable.SetApplyPlayableIK(false);

            clipPlayable.SetSpeed(1);

            clipPlayable.SetTime(0);


            AnimationPlayableOutput output =
                AnimationPlayableOutput.Create(
                    graph,
                    "AnimationOutput",
                    animator);

            output.SetSourcePlayable(
                clipPlayable);


            graph.Play();


            // =========================================================
            // 6. 初始化到动画 0 秒
            // =========================================================

            /*
             * 这里非常重要。
             *
             * 不要一创建 Graph 就直接采第一帧。
             *
             * 先 Evaluate(0)，
             * 让 Animator 进入动画起始状态。
             */
            graph.Evaluate(0f);


            /*
             * 动画初始化后的朝向，
             * 作为整个动画的“初始坐标系”。
             */
            Quaternion initialRotation =
                animator.transform.rotation;

            Quaternion worldToInitialLocal =
                Quaternion.Inverse(initialRotation);


            // =========================================================
            // 7. 创建数据
            // =========================================================

            RootMotionBakeData bakeData =
                CreateInstance<RootMotionBakeData>();

            bakeData.clip = animationClip;
            bakeData.sampleRate = sampleRate;
            bakeData.precision = precision;


            float frameDeltaTime =
                1f / sampleRate;

            float currentTime = 0;

            Vector3 accumulatedPosition =
                Vector3.zero;


            /*
             * 整数累计位置。
             *
             * 后面不是：
             *
             * Round(delta) -> 累加
             *
             * 而是：
             *
             * 浮点位置累加
             * -> Round(累计位置)
             * -> 当前整数位置 - 上一帧整数位置
             *
             * 这样可以显著减少累计误差。
             */
            Vector3Int previousIntPosition =
                Vector3Int.zero;


            // =========================================================
            // 8. 手动逐帧推进
            // =========================================================

            while (currentTime < animationClip.length)
            {
                float remain =
                    animationClip.length - currentTime;

                float dt =
                    Mathf.Min(
                        frameDeltaTime,
                        remain);


                // -----------------------------
                // 真正让动画向前走 dt
                // -----------------------------

                graph.Evaluate(dt);


                currentTime += dt;


                // =====================================================
                // 9. 获取 Animator RootMotion
                // =====================================================

                /*
                 * Animator.deltaPosition：
                 *
                 * 表示从“上一次 Animator Evaluate”
                 * 到“这一次 Evaluate”
                 *
                 * Root Motion 根节点移动了多少。
                 */
                Vector3 worldDelta =
                    animator.deltaPosition;


                // =====================================================
                // 10. 转换到初始朝向坐标系
                // =====================================================

                /*
                 * 举例：
                 *
                 * 角色初始朝向是世界 X 方向。
                 *
                 * 世界位移：
                 *
                 * (1,0,0)
                 *
                 * 转到角色初始局部空间后可能就是：
                 *
                 * (0,0,1)
                 *
                 * 也就是说：
                 *
                 * Z 永远代表动画开始时的“前方”。
                 */
                Vector3 localDelta =
                    worldToInitialLocal *
                    worldDelta;


                // =====================================================
                // 11. 累加位置
                // =====================================================

                accumulatedPosition +=
                    localDelta;


                // =====================================================
                // 12. 浮点 -> 整数
                // =====================================================

                Vector3Int currentIntPosition =
                    new Vector3Int(
                        Mathf.RoundToInt(
                            accumulatedPosition.x * precision),

                        Mathf.RoundToInt(
                            accumulatedPosition.y * precision),

                        Mathf.RoundToInt(
                            accumulatedPosition.z * precision)
                    );


                /*
                 * 用整数位置之差得到整数位移。
                 *
                 * 不要直接：
                 *
                 * Round(localDelta * precision)
                 *
                 * 否则每一帧都产生独立舍入误差，
                 * 最终可能累计出明显偏差。
                 */
                Vector3Int deltaInt =
                    currentIntPosition -
                    previousIntPosition;


                // =====================================================
                // 13. 保存这一逻辑帧
                // =====================================================

                RootMotionFrameData frameData =
                    new RootMotionFrameData();

                frameData.time =
                    currentTime;

                frameData.deltaPosition =
                    localDelta;

                frameData.position =
                    accumulatedPosition;

                frameData.deltaPositionInt =
                    deltaInt;

                frameData.positionInt =
                    currentIntPosition;


                bakeData.frames.Add(
                    frameData);


                previousIntPosition =
                    currentIntPosition;
            }


            // =========================================================
            // 14. 保存 Asset
            // =========================================================

            string path =
                EditorUtility.SaveFilePanelInProject(
                    "保存 RootMotion 数据",
                    animationClip.name + "_RootMotion",
                    "asset",
                    "请选择保存位置");

            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(
                    bakeData,
                    path);

                AssetDatabase.SaveAssets();

                AssetDatabase.Refresh();

                Selection.activeObject =
                    bakeData;
            }


            Debug.Log(
                $"RootMotion 烘焙完成：" +
                $"Clip={animationClip.name} " +
                $"Frames={bakeData.frames.Count} " +
                $"FinalPosition={accumulatedPosition}");
        }
        finally
        {
            // =========================================================
            // 清理
            // =========================================================

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


    /// <summary>
    /// 禁止所有可能干扰 RootMotion 的组件
    /// </summary>
    private void DisableOtherComponents(
        GameObject character,
        Animator targetAnimator)
    {
        // ----------------------------------
        // MonoBehaviour
        // ----------------------------------

        MonoBehaviour[] scripts =
            character.GetComponentsInChildren
            <MonoBehaviour>(true);

        foreach (MonoBehaviour script in scripts)
        {
            script.enabled = false;
        }


        // ----------------------------------
        // 其他 Behaviour
        // ----------------------------------

        Behaviour[] behaviours =
            character.GetComponentsInChildren
            <Behaviour>(true);

        foreach (Behaviour behaviour in behaviours)
        {
            if (behaviour == targetAnimator)
                continue;

            behaviour.enabled = false;
        }


        // ----------------------------------
        // 3D Rigidbody
        // ----------------------------------

        Rigidbody[] rigidbodies =
            character.GetComponentsInChildren
            <Rigidbody>(true);

        foreach (Rigidbody rb in rigidbodies)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.detectCollisions = false;

            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }


        // ----------------------------------
        // Collider
        // ----------------------------------

        Collider[] colliders =
            character.GetComponentsInChildren
            <Collider>(true);

        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }


        // ----------------------------------
        // Rigidbody2D
        // ----------------------------------

        Rigidbody2D[] rigidbody2Ds =
            character.GetComponentsInChildren
            <Rigidbody2D>(true);

        foreach (Rigidbody2D rb in rigidbody2Ds)
        {
            rb.simulated = false;
        }


        Collider2D[] collider2Ds =
            character.GetComponentsInChildren
            <Collider2D>(true);

        foreach (Collider2D collider in collider2Ds)
        {
            collider.enabled = false;
        }
    }
}