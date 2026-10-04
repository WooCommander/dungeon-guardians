using System.Collections.Generic;
using UnityEngine;

namespace DungeonGuardians.Presentation
{
    public enum CharacterPose
    {
        // Walk or Idle, chosen by whether the character is moving.
        Ground,
        Ladder,
        Bar,
        Fall,
        Dig,
        Struggle,
        Dead
    }

    // Shows a character model from Resources/Characters, glides it between grid cells and picks the animation clip.
    public sealed class CharacterView : MonoBehaviour
    {
        // Models face +Z; the camera looks along +Z, so yaw 0 shows the back and 180 the front.
        private const float SideYaw = 125f;
        private const float BackYaw = 0f;
        private const float TurnSpeed = 900f;
        private const float CrossFade = 0.12f;
        // Keeps Walk playing through the short pauses between cell steps.
        private const float MoveGrace = 0.08f;
        private const float SnapDistance = 1.5f;

        private readonly Dictionary<string, string> clipNames = new Dictionary<string, string>();
        private Animation animationPlayer;
        private Vector3 target;
        private float speed;
        private bool snapNext = true;
        private float lastMoveTime = -1f;
        private int facing = 1;
        private CharacterPose pose;
        private string currentClip;

        public static CharacterView Create(string modelName, Transform parent, float height, float speed, Color fallbackColor)
        {
            var root = new GameObject(modelName);
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<CharacterView>();
            view.speed = speed;
            view.Build(modelName, height, fallbackColor);
            return view;
        }

        // position is the bottom centre of the occupied cell.
        public void SetTarget(Vector3 position)
        {
            if (!snapNext && Mathf.Abs(position.x - target.x) > 0.01f)
            {
                facing = position.x > target.x ? 1 : -1;
            }

            target = position;
            if (snapNext || Vector3.Distance(transform.position, target) > SnapDistance)
            {
                transform.position = target;
                snapNext = false;
            }
        }

        public void SetPose(CharacterPose value, int facingOverride = 0)
        {
            if (facingOverride != 0)
            {
                facing = facingOverride;
            }

            if (value == CharacterPose.Dig && pose != CharacterPose.Dig)
            {
                // Restart the swing for every new dig.
                currentClip = null;
            }

            pose = value;
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
                snapNext = true;
            }
        }

        public void SnapNextMove()
        {
            snapNext = true;
        }

        // Fits a one-shot clip (Dig) into the gameplay duration so the visible swing matches the input lock.
        public void SetOneShotDuration(string clip, float duration)
        {
            if (animationPlayer != null && clipNames.TryGetValue(clip, out string state) && duration > 0f)
            {
                animationPlayer[state].speed = animationPlayer[state].length / duration;
            }
        }

        private void Update()
        {
            Vector3 before = transform.position;
            transform.position = Vector3.MoveTowards(before, target, speed * Time.deltaTime);
            if ((transform.position - before).sqrMagnitude > 0f)
            {
                lastMoveTime = Time.time;
            }

            bool moving = Time.time - lastMoveTime < MoveGrace;
            UpdateFacing();
            UpdateAnimation(moving);
        }

        private void UpdateFacing()
        {
            float yaw = pose == CharacterPose.Ladder ? BackYaw : SideYaw * facing;
            Quaternion wanted = Quaternion.Euler(0f, yaw, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, TurnSpeed * Time.deltaTime);
        }

        private void UpdateAnimation(bool moving)
        {
            if (animationPlayer == null)
            {
                return;
            }

            switch (pose)
            {
                case CharacterPose.Ground:
                    Play(moving ? "Walk" : "Idle", 1f);
                    break;
                case CharacterPose.Ladder:
                case CharacterPose.Bar:
                    // No dedicated hang clip yet: the climb cycle is reused on bars and frozen while standing still.
                    Play("Climb", moving ? 1f : 0f);
                    break;
                case CharacterPose.Fall:
                    Play("Fall", 1f);
                    break;
                case CharacterPose.Dig:
                    Play("Dig", -1f);
                    break;
                case CharacterPose.Struggle:
                    Play("Struggle", 1f);
                    break;
                case CharacterPose.Dead:
                    if (currentClip != null && clipNames.TryGetValue(currentClip, out string state))
                    {
                        animationPlayer[state].speed = 0f;
                    }

                    break;
            }
        }

        // speed < 0 keeps the speed set by SetOneShotDuration.
        private void Play(string clip, float clipSpeed)
        {
            if (!clipNames.TryGetValue(clip, out string state))
            {
                return;
            }

            if (clipSpeed >= 0f)
            {
                animationPlayer[state].speed = clipSpeed;
            }

            if (currentClip == clip)
            {
                return;
            }

            if (clip == "Dig")
            {
                animationPlayer[state].time = 0f;
                animationPlayer.Play(state);
            }
            else
            {
                animationPlayer.CrossFade(state, CrossFade);
            }

            currentClip = clip;
        }

        private void Build(string modelName, float height, Color fallbackColor)
        {
            GameObject prefab = Resources.Load<GameObject>($"Characters/{modelName}");
            GameObject model;
            if (prefab != null)
            {
                model = Instantiate(prefab, transform, false);
            }
            else
            {
                Debug.LogError($"Character model Resources/Characters/{modelName} not found; using a placeholder cube.");
                model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                model.transform.SetParent(transform, false);
                model.GetComponent<Renderer>().material.color = fallbackColor;
            }

            model.name = "Model";
            FitToHeight(model, height);
            SetUpAnimation(model);
        }

        // The source models are about 2 m tall; scale them to the requested height in cells and stand them on the origin.
        private void FitToHeight(GameObject model, float height)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer item in renderers)
            {
                bounds.Encapsulate(item.bounds);
            }

            float scale = height / Mathf.Max(bounds.size.y, 0.001f);
            model.transform.localScale *= scale;
            float bottom = (bounds.min.y - transform.position.y) * scale;
            model.transform.localPosition -= new Vector3(0f, bottom, 0f);

            foreach (SkinnedMeshRenderer skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                // Animated poses can leave the bind-pose bounds; avoid the model popping out at screen edges.
                skinned.updateWhenOffscreen = true;
            }
        }

        private void SetUpAnimation(GameObject model)
        {
            animationPlayer = model.GetComponentInChildren<Animation>();
            if (animationPlayer == null)
            {
                return;
            }

            animationPlayer.playAutomatically = false;
            foreach (AnimationState state in animationPlayer)
            {
                // FBX takes are named "Armature|Clip"; keep the clip part.
                string clip = state.name.Substring(state.name.LastIndexOf('|') + 1);
                clipNames[clip] = state.name;
                state.wrapMode = clip == "Dig" || clip == "Fall" ? WrapMode.ClampForever : WrapMode.Loop;
            }
        }
    }
}
