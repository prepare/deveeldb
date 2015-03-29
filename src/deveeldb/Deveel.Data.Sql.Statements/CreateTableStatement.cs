﻿using System;
using System.Collections.Generic;
using System.Linq;

using Deveel.Data.DbSystem;

namespace Deveel.Data.Sql.Statements {
	[Serializable]
	public sealed class CreateTableStatement : Statement {
		internal CreateTableStatement() {
		}

		public CreateTableStatement(string tableName, IEnumerable<ColumnInfo> columns) {
			TableName = tableName;

			if (columns != null) {
				foreach (var column in columns) {
					Columns.Add(column);
				}
			}
		}

		public string TableName {
			get { return GetValue<string>(Keys.TableName); }
			private set { SetValue(Keys.TableName, value); }
		}

		public IList<ColumnInfo> Columns {
			get { return GetList<ColumnInfo>(Keys.Columns); }
		}

		public bool IfNotExists {
			get { return GetValue<bool>(Keys.IfNotExists); }
			set { SetValue(Keys.IfNotExists, value); }
		}

		public bool Temporary {
			get { return GetValue<bool>(Keys.Temporary); }
			set { SetValue(Keys.Temporary, value); }
		}

		protected override PreparedStatement OnPrepare(IQueryContext context) {
			throw new NotImplementedException();
		}

		#region PreparedCreateTableStatement

		class PreparedCreateTableStatement : PreparedStatement {
			public ObjectName TableName { get; set; }

			public bool Temporary { get; set; }

			public bool IfNotExists { get; set; }

			public IEnumerable<ColumnInfo> Columns { get; set; } 

			protected override ITable OnEvaluate(IQueryContext context) {
				throw new NotImplementedException();
			}
		}

		#endregion

		#region Keys

		internal static class Keys {
			public const string TableName = "TableName";
			public const string Columns = "Columns";
			public const string IfNotExists = "IfNotExists";
			public const string Temporary = "Temporary";
		}

		#endregion
	}
}
