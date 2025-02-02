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
using System.Collections.Generic;

namespace Deveel.Data.Sql.Parser {
	class AlterTableNode : SqlNode {
		internal AlterTableNode() {
		}

		public ObjectNameNode TableName { get; private set; }

		public IEnumerable<AlterTableActionNode> Actions { get; private set; }

		public CreateTableNode CreateTable { get; private set; }

		protected override ISqlNode OnChildNode(ISqlNode node) {
			return base.OnChildNode(node);
		}
	}
}
