using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RaActions
{
	public static class RaActionUtils
	{
		public static bool IsSuccessful(this IRaActionResult result)
		{
			return result != null && result.Success;
		}
	}
}