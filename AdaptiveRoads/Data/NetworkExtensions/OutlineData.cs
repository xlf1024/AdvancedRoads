namespace AdaptiveRoads.Data.NetworkExtensions {
    using ColossalFramework.Math;
    using UnityEngine;
    using KianCommons.Math;

    public struct OutlineData {
        public Bezier3 Center, Left, Right;
        public Vector3 DirA, DirD;
        public bool SmoothA, SmoothD;

        public bool Empty => Center.a == Center.d;

        // TODO: should I just raise the lane instead of accepting deltaY
        /// <param name="angle">tilt angle in radians</param>
        public OutlineData(Vector3 a, Vector3 d, Vector3 dirA, Vector3 dirD, float width, bool smoothA, bool smoothD, float angleA, float angleD) {
            //bool nodeless = (a - d).sqrMagnitude < 0.01; // too small to render
            //if(!nodeless) {
            //    // check if lane ends is in the direction of dirA or dirD
            //    Vector2 dir = (a - d).ToCS2D().normalized;
            //    Vector2 dir1 = dirA.ToCS2D().normalized;
            //    Vector2 dir2 = dirD.ToCS2D().normalized;
            //    float absdot0 = Mathf.Abs(Vector2.Dot(dir1, dir2)); // if dirs are aligned, we expect the lane ends to be aligned too even if not nodeless.
            //    float absdot1 = Mathf.Abs(Vector2.Dot(dir, dir1));
            //    float absdot2 = Mathf.Abs(Vector2.Dot(dir, dir2));
            //    nodeless = absdot0 < 0.999 && (absdot1 > 0.999 || absdot2 > 0.999); 
            //}

            //if(nodeless) {
            //    Center = Left = Right = default;
            //    DirA = DirD = default;
            //    SmoothA = SmoothD = default;
            //    return;
            //}

            float hw = 0.5f * width;

            SmoothA = smoothA;
            Center.a = a;
            DirA = dirA;
            var normalA = new Vector3(dirA.z, 0, -dirA.x);
            normalA = VectorUtils.NormalizeXZ(normalA); // rotate right.
            Right.a = a + normalA * hw;
            Left.a = a - normalA * hw;

            SmoothD = smoothD;
            DirD = dirD;
            Center.d = d;
            var normalD = new Vector3(dirD.z, 0, -dirD.x); // rotate right.
            normalD = -VectorUtils.NormalizeXZ(normalD); // end dir needs minus
            Right.d = d + normalD * hw;
            Left.d = d - normalD * hw;

            {
                Right.a.y += hw * Mathf.Sin(angleA);
                Left.a.y -= hw * Mathf.Sin(angleA);
                Right.d.y += hw * Mathf.Sin(angleD);
                Left.d.y -= hw * Mathf.Sin(angleD);

                Right.a += hw * normalA * Mathf.Cos(angleA);
                Left.a -= hw * normalA * Mathf.Cos(angleA);
                Right.d += hw * normalD * Mathf.Cos(angleD);
                Left.d -= hw * normalD * Mathf.Cos(angleD);
            }

            NetSegment.CalculateMiddlePoints(Center.a, DirA, Center.d, DirD, SmoothA, SmoothD, out Center.b, out Center.c);
            NetSegment.CalculateMiddlePoints(Left.a, DirA, Left.d, DirD, SmoothA, SmoothD, out Left.b, out Left.c);
            NetSegment.CalculateMiddlePoints(Right.a, DirA, Right.d, DirD, SmoothA, SmoothD, out Right.b, out Right.c);
        }
    }
}
