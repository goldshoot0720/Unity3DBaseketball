using UnityEngine;

namespace MiaCourt
{
    public sealed class CourtCamera : MonoBehaviour
    {
        MiaBasketballGame game;
        Camera cam;
        Material backdrop;
        Transform plate;
        public int view = 1;

        public void Initialize(MiaBasketballGame owner)
        {
            game = owner;
            cam = GetComponent<Camera>();
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "Original reference backdrop - left / center / right";
            var bgCollider = bg.GetComponent<Collider>();
            if (bgCollider != null) Destroy(bgCollider);
            bg.transform.SetParent(transform,false);
            bg.transform.localPosition = new Vector3(0,0,120);
            plate = bg.transform;
            backdrop = new Material(game.assets.backdropShader);
            bg.GetComponent<Renderer>().sharedMaterial = backdrop;
            bg.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bg.GetComponent<Renderer>().receiveShadows = false;
            SetView(1);
            Snap();
        }

        public void SetView(int index)
        {
            view = Mathf.Clamp(index,0,2);
            backdrop.mainTexture = view == 0 ? game.assets.leftView : view == 2 ? game.assets.rightView : game.assets.centerView;
        }

        public void Snap() => Position(1f);
        void LateUpdate() => Position(1f - Mathf.Exp(-Time.unscaledDeltaTime * 3.5f));

        void Position(float blend)
        {
            bool lobby = game.State == MatchState.Home;
            // Close enough that the cats read clearly; the pan has to track harder at this distance
            // or a player driving to the hoop leaves the frame.
            float follow = !lobby && game.players != null ? (game.players[0].transform.position.x + game.ballBody.position.x) * .22f : 0;
            Vector3 position = lobby ? new Vector3(0,5.2f,-12.8f) : new Vector3((view-1)*3.0f + follow,7.2f,-12.0f);
            Vector3 look = lobby ? new Vector3(.3f,1.9f,1.4f) : new Vector3(follow,1.0f,.6f);
            transform.position = Vector3.Lerp(transform.position,position,blend);
            transform.rotation = Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(look-position),blend);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView,lobby ? 44 : 46,blend);
            float h = 2 * 120 * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2);
            plate.localScale = new Vector3(h * cam.aspect, h,1);
        }
    }
}
