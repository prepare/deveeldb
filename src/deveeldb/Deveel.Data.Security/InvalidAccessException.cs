﻿// 
//  Copyright 2010-2015 Deveel
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//

using System;

namespace Deveel.Data.Security {
	[Serializable]
	public class InvalidAccessException : SecurityException {
		public InvalidAccessException(ObjectName objectName)
			: this(objectName, BuildMessage(objectName)) {
		}

		public InvalidAccessException(ObjectName objectName, string message)
			: base(SecurityErrorCodes.InvalidAccess, message) {
			ObjectName = objectName;
		}

		public ObjectName ObjectName { get; private set; }

		private static string BuildMessage(ObjectName objectName) {
			if (objectName == null)
				return "Cannot access the object: possibly not enough privileges.";

			return String.Format("Cannot access the '{0}': possibly not enough privileges.", objectName);
		}
	}
}
