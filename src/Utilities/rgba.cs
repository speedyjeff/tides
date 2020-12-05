using System;

namespace Utilities
{
	public struct RGBA
	{
		public byte R;
		public byte G;
		public byte B;
		public byte A;

		public static RGBA Black = new RGBA() { R = 0, G = 0, B = 0, A = 255 };
		public static RGBA White = new RGBA() { R = 255, G = 255, B = 255, A = 255 };

		public override int GetHashCode()
		{
			unchecked
			{
				var result = 0;
				result = (result * 31) ^ R;
				result = (result * 31) ^ G;
				result = (result * 31) ^ B;
				result = (result * 31) ^ A;
				return result;
			}
		}
	}
}