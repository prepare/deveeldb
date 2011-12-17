// 
//  Copyright 2010  Deveel
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

using System;
using System.Collections.Generic;

using Deveel.Data.Functions;
using Deveel.Data.QueryPlanning;
using Deveel.Diagnostics;

namespace Deveel.Data {
	/// <summary>
	/// An abstract implementation of <see cref="IQueryContext"/>
	/// </summary>
	public abstract class QueryContext : IQueryContext {
		private readonly EmptyDebugLogger emptyLogger = new EmptyDebugLogger();

		/// <summary>
		/// Any marked tables that are made during the evaluation of a query plan.
		/// </summary>
		private Dictionary<string, Table> markedTables;

		/// <inheritdoc/>
		public virtual TransactionSystem System {
			get { return (Connection == null ? null : Connection.System); }
		}

		/// <inheritdoc/>
		public virtual string UserName {
			get { return (Connection == null ? null : Connection.User.UserName); }
		}

		public virtual DatabaseConnection Connection {
			get { return null; }
		}

		/// <inheritdoc/>
		public virtual IFunctionLookup FunctionLookup {
			get { return Connection == null ? null : Connection.System.FunctionLookup; }
		}

		public virtual IDebugLogger Debug {
			get { return Connection == null ? emptyLogger : Connection.Debug; }
		}

		public virtual Table GetTable(TableName tableName) {
			Connection.AddSelectedFromTable(tableName);
			return Connection.GetTable(tableName);
		}

		public virtual Privileges GetUserGrants(GrantObject objType, string objName) {
			return Connection.GrantManager.GetUserGrantOptions(objType, objName, UserName);
		}

		/// <inheritdoc/>
		public virtual long NextSequenceValue(string generatorName) {
			return Connection.NextSequenceValue(generatorName);
		}

		/// <inheritdoc/>
		public virtual long CurrentSequenceValue(string generatorName) {
			return Connection.LastSequenceValue(generatorName);
		}

		/// <inheritdoc/>
		public virtual void SetSequenceValue(string generatorName, long value) {
			Connection.SetSequenceValue(generatorName, value);
		}

		/// <inheritdoc/>
		public void AddMarkedTable(string markName, Table table) {
			if (markedTables == null)
				markedTables = new Dictionary<string, Table>();

			markedTables.Add(markName, table);
		}

		/// <inheritdoc/>
		public Table GetMarkedTable(string markName) {
			if (markedTables == null)
				return null;
			Table table;
			if (!markedTables.TryGetValue(markName, out table))
				return null;

			return table;
		}

		/// <inheritdoc/>
		public void PutCachedNode(long id, Table table) {
			AddMarkedTable(id.ToString(), table);
		}

		/// <inheritdoc/>
		public Table GetCachedNode(long id) {
			return GetMarkedTable(id.ToString());
		}

		/// <inheritdoc/>
		public void ClearCache() {
			if (markedTables != null)
				markedTables.Clear();
		}

		public virtual Variable DeclareVariable(string name, TType type, bool constant, bool notNull) {
			return null;
		}

		public virtual Variable GetVariable(string name) {
			return null;
		}

		public virtual void SetVariable(string name, Expression value) {
			// nothing to do by default...
		}

		public virtual void RemoveVariable(string name) {
			// nothing to do by default...
		}

		/// <inheritdoc/>
		public virtual Cursor DeclareCursor(TableName name, IQueryPlanNode planNode, CursorAttributes attributes) {
			return null;
		}

		/// <inheritdoc/>
		public virtual Cursor GetCursor(TableName name) {
			return null;
		}

		/// <inheritdoc/>
		public virtual void OpenCursor(TableName name) {
		}

		/// <inheritdoc/>
		public virtual void CloseCursor(TableName name) {
		}
	}
}