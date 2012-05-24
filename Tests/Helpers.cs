using System;
using System.Text;
using System.Linq;

namespace AntMicro.AntSerializer.Tests
{
	public static class Helpers
	{
		public static string GetRandomString(int length)
		{
			var builder = new StringBuilder(length);
			for(var i = 0; i < length; i++)
			{
				char character;
				do
				{
					character = (char)Random.Next(0xFF + 1);
				} while(char.IsControl(character));
				builder.Append(character);
			}
			return builder.ToString();
		}

		public static int[] GetRandomIntegers(int numberOfInts, int exclusiveMaximum = int.MaxValue)
		{
			var randomInts = new int[numberOfInts];
			for(var i = 0; i < randomInts.Length; i++)
			{
				randomInts[i] = Random.Next(exclusiveMaximum) - Random.Next(exclusiveMaximum);
			}
			return randomInts;
		}

		public static long[] GetRandomLongs(int numberOfLongs, long exclusiveMaximum = long.MaxValue)
		{
			int lowMax, hiMax;
			if(exclusiveMaximum > int.MaxValue)
			{
				lowMax = int.MaxValue;
				hiMax = (int)((exclusiveMaximum>>32)-1);
			}
			else
			{
				hiMax = 0;
				lowMax = (int)exclusiveMaximum;
			}
			var randomLongs = new long[numberOfLongs];
			for(var i = 0; i < randomLongs.Length; i++)
			{
				randomLongs[i] = ((long)Random.Next(hiMax)<<32) + Random.Next(lowMax);
			}
			return randomLongs;
		}

		public static double[] GetRandomDoubles(int numberOfDoubles, double exclusiveMaximum = double.MaxValue)
		{
			var randomDoubles = new double[numberOfDoubles];
			for(var i = 0; i < randomDoubles.Length; i++)
			{
				randomDoubles[i] = (Random.NextDouble() - 0.5) * exclusiveMaximum;
			}
			return randomDoubles;
		}

		public static TimeSpan[] GetRandomTimeSpans(int numberOfTimeSpans)
		{
			return GetRandomLongs(numberOfTimeSpans, TimeSpan.MaxValue.Ticks).Select(x=>TimeSpan.FromTicks(x)).ToArray();
		}

		public static DateTime[] GetRandomDateTimes(int numberOfDateTimes)
		{
			return GetRandomLongs(numberOfDateTimes, DateTime.MaxValue.Ticks).Select(x=>new DateTime(x)).ToArray();
		}

		public static Random Random { get; private set; }

		static Helpers()
		{
			Random = new Random();
		}
	}
}
