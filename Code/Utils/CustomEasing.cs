namespace ShrimpleRagdolls;

public static class CustomEasing
{
	extension( Sandbox.Utility.Easing )
	{
		/// <summary>
		/// Easing function that anticipates backward before moving forward with a slight overshoot.
		/// Creates a smooth animation that pulls back, overshoots the target, then settles.
		/// </summary>
		/// <param name="t">Time value between 0 to 1</param>
		/// <returns>Eased value</returns>
		public static float AnticipateOvershoot( float t )
		{
			const float anticipate = 1.70158f * 4f;  // Larger value = more anticipation
			const float overshoot = 1.70158f * 2f;   // Smaller value = less overshoot

			t /= 0.5f;
			if ( t < 1f ) return 0.5f * (t * t * ((anticipate + 1f) * t - anticipate));
			t -= 2f;
			return 0.5f * (t * t * ((overshoot + 1f) * t + overshoot) + 2f);
		}
	}
}
