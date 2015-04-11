﻿using System;

using Deveel.Data.DbSystem;
using Deveel.Data.Protocol;
using Deveel.Data.Sql;
using Deveel.Data.Sql.Expressions;
using Deveel.Data.Transactions;

namespace Deveel.Data.Security {
	public static class SecurityQueryExtensions {
		private static bool UserCanAccessFromHost(this IQueryContext queryContext, string username, ConnectionEndPoint endPoint) {
			// The system user is not allowed to login
			if (String.Equals(username, User.SystemName, StringComparison.OrdinalIgnoreCase))
				return false;

			// What's the protocol?
			string protocol = endPoint.Protocol;
			string host = endPoint.Address;

			// The table to check
			var connectPriv = queryContext.GetTable(SystemSchema.UserConnectPrivilegesTableName);
			var unCol = connectPriv.GetResolvedColumnName(0);
			var protoCol = connectPriv.GetResolvedColumnName(1);
			var hostCol = connectPriv.GetResolvedColumnName(2);
			var accessCol = connectPriv.GetResolvedColumnName(3);

			// Query: where UserName = %username%
			var t = connectPriv.SimpleSelect(queryContext, unCol, BinaryOperator.Equal, SqlExpression.Constant(username));
			// Query: where %protocol% like Protocol
			var exp = SqlExpression.Binary(SqlExpression.Constant(protocol), SqlExpressionType.Like, SqlExpression.Reference(protoCol));
			t = t.ExhaustiveSelect(queryContext, exp);
			// Query: where %host% like Host
			exp = SqlExpression.Binary(SqlExpression.Constant(host), SqlExpressionType.Like, SqlExpression.Reference(hostCol));
			t = t.ExhaustiveSelect(queryContext, exp);

			// Those that are DENY
			var t2 = t.SimpleSelect(queryContext, accessCol, BinaryOperator.Equal, SqlExpression.Constant(DataObject.BooleanFalse));
			if (t2.RowCount > 0)
				return false;

			// Those that are ALLOW
			var t3 = t.SimpleSelect(queryContext, accessCol, BinaryOperator.Equal, SqlExpression.Constant(DataObject.BooleanTrue));
			if (t3.RowCount > 0)
				return true;

			// No DENY or ALLOW entries for this host so deny access.
			return false;
		}

		public static bool UserBelongsToGroup(this IQueryContext queryContext, string username, string group) {
			// This is a special query that needs to access the lowest level of ITable, skipping
			// other security controls
			var table = queryContext.Session.Transaction.GetTable(SystemSchema.UserPrivilegesTableName);
			var c1 = table.GetResolvedColumnName(0);
			var c2 = table.GetResolvedColumnName(1);
			// All 'user_priv' where UserName = %username%
			var t = table.SimpleSelect(queryContext, c1, BinaryOperator.Equal, SqlExpression.Constant(username));
			// All from this set where PrivGroupName = %group%
			t = t.SimpleSelect(queryContext, c2, BinaryOperator.Equal, SqlExpression.Constant(group));
			return t.RowCount > 0;
		}

		public static void AddUserToGroup(this IQueryContext queryContext, string username, string group) {
			if (String.IsNullOrEmpty(group))
				throw new ArgumentNullException("group");
			if (String.IsNullOrEmpty(username))
				throw new ArgumentNullException("username");

			char c = group[0];
			if (c == '@' || c == '&' || c == '#' || c == '$')
				throw new ArgumentException(String.Format("Group name '{0}' is invalid: cannot start with {1}", group, c), "group");

			if (!queryContext.UserBelongsToGroup(username, group)) {
				var table = queryContext.GetMutableTable(SystemSchema.UserPrivilegesTableName);
				var row = table.NewRow();
				row.SetValue(0, username);
				row.SetValue(1, group);
				table.AddRow(row);
			}
		}

		public static void SetUserLock(this IQueryContext queryContext, string username, bool lockStatus) {
			// Internally we implement this by adding the user to the #locked group.
			var table = queryContext.GetMutableTable(SystemSchema.UserPrivilegesTableName);
			var c1 = table.GetResolvedColumnName(0);
			var c2 = table.GetResolvedColumnName(1);
			// All 'user_priv' where UserName = %username%
			var t = table.SimpleSelect(queryContext, c1, BinaryOperator.Equal, SqlExpression.Constant(username));
			// All from this set where PrivGroupName = %group%
			t = t.SimpleSelect(queryContext, c2, BinaryOperator.Equal, SqlExpression.Constant(SystemGroupNames.LockGroup));

			bool userBelongsToLockGroup = t.RowCount > 0;
			if (lockStatus && !userBelongsToLockGroup) {
				// Lock the user by adding the user to the Lock group
				// Add this user to the locked group.
				var rdat = new Row(table);
				rdat.SetValue(0, username);
				rdat.SetValue(1, SystemGroupNames.LockGroup);
				table.AddRow(rdat);
			} else if (!lockStatus && userBelongsToLockGroup) {
				// Unlock the user by removing the user from the Lock group
				// Remove this user from the locked group.
				table.Delete(t);
			}
		}

		public static void GrantHostAccessToUser(this IQueryContext queryContext, string user, string protocol, string host) {
			// The user connect priv table
			var table = queryContext.GetMutableTable(SystemSchema.UserConnectPrivilegesTableName);
			// Add the protocol and host to the table
			var rdat = new Row(table);
			rdat.SetValue(0, user);
			rdat.SetValue(1, protocol);
			rdat.SetValue(2, host);
			rdat.SetValue(3, true);
			table.AddRow(rdat);
		}

		public static bool UserExists(this IQueryContext context, string userName) {
			var table = context.GetTable(SystemSchema.UserTableName);
			var c1 = table.GetResolvedColumnName(0);

			// All password where UserName = %username%
			var t = table.SimpleSelect(context, c1, BinaryOperator.Equal, SqlExpression.Constant(userName));
			return t.RowCount > 0;
		}

		public static void GrantToUserOn(this IQueryContext context, DbObjectType objectType, ObjectName objectName,
			User user, Privileges privileges, bool withOption = false) {
			GrantToUserOn(context, objectType, objectName, user, context.User(), privileges, withOption);
		}

		public static void GrantToUserOn(this IQueryContext context, DbObjectType objectType, ObjectName objectName,
			User user, User granter, Privileges privileges, bool withOption = false) {
			if (!context.ObjectExists(objectType, objectName))
				throw new ObjectNotFoundException(objectName);

			Privileges oldPrivs = context.GetUserGrants(user, objectType, objectName);
			privileges |= oldPrivs;

			if (!oldPrivs.Equals(privileges))
				context.UpdateUserGrants(objectType, objectName, user, granter, privileges, withOption);
		}

		public static void GrantToUserOnSchemaTables(this IQueryContext context, string schemaName, User user, User granter,
			Privileges privileges) {
			context.GrantToUserOnSchemaObjects(schemaName, DbObjectType.Table, user, granter, privileges);
		}

		public static void GrantToUserOnSchemaObjects(this IQueryContext context, string schemaName, DbObjectType objectType, User user,
			User granter, Privileges privileges) {
			// TODO: Query for all objects of the given type in the schema
 			//       and grant the given privileges..
		}

		private static void UpdateUserGrants(this IQueryContext context, DbObjectType objectType, ObjectName objectName,
			User user, User granter, Privileges privileges, bool withOption) {
			// Revoke existing privs on this object for this grantee
			context.RevokeAllFromUserOn(objectType, objectName, user, granter, withOption);

			if (privileges != Privileges.None) {
				// The system grants table.
				var grantTable = context.GetMutableTable(SystemSchema.UserGrantsTableName);

				// Add the grant to the grants table.
				var rdat = grantTable.NewRow();
				rdat.SetValue(0, (int)privileges);
				rdat.SetValue(1, (int)objectType);
				rdat.SetValue(2, objectName.FullName);
				rdat.SetValue(3, user.Name);
				rdat.SetValue(4, withOption);
				rdat.SetValue(5, granter.Name);
				grantTable.AddRow(rdat);

				user.CacheObjectGrant(objectName, privileges);
			}
		}

		public static void RevokeAllFromUserOn(this IQueryContext context, DbObjectType objectType, ObjectName objectName,
			User user, User revoker, bool withOption = false) {
			context.RevokeAllGrants(objectType, objectName, user, revoker, withOption);
		}

		private static void RevokeAllGrants(this IQueryContext context, DbObjectType objectType, ObjectName objectName,
			User user, User revoker, bool withOption = false) {
			var grantTable = context.GetMutableTable(SystemSchema.UserGrantsTableName);

			var objectCol = grantTable.GetResolvedColumnName(1);
			var paramCol = grantTable.GetResolvedColumnName(2);
			var granteeCol = grantTable.GetResolvedColumnName(3);
			var grantOptionCol = grantTable.GetResolvedColumnName(4);
			var granterCol = grantTable.GetResolvedColumnName(5);

			ITable t1 = grantTable;

			// All that match the given object parameter
			// It's most likely this will reduce the search by the most so we do
			// it first.
			t1 = t1.SimpleSelect(context, paramCol, BinaryOperator.Equal,
									   SqlExpression.Constant(DataObject.String(objectName.FullName)));

			// The next is a single exhaustive select through the remaining records.
			// It finds all grants that match either public or the grantee is the
			// username, and that match the object type.

			// Expression: ("grantee_col" = username)
			var userCheck = SqlExpression.Equal(SqlExpression.Reference(granteeCol),
				SqlExpression.Constant(DataObject.String(user.Name)));

			// Expression: ("object_col" = object AND
			//              "grantee_col" = username)
			// All that match the given username or public and given object
			var expr =
				SqlExpression.And(
					SqlExpression.Equal(SqlExpression.Reference(objectCol),
						SqlExpression.Constant(DataObject.BigInt((int) objectType))), userCheck);

			// Are we only searching for grant options?
			var grantOptionCheck = SqlExpression.Equal(SqlExpression.Reference(grantOptionCol),
				SqlExpression.Constant(DataObject.Boolean(withOption)));
			expr = SqlExpression.And(expr, grantOptionCheck);

			// Make sure the granter matches up also
			var granterCheck = SqlExpression.Equal(SqlExpression.Reference(granterCol),
				SqlExpression.Constant(DataObject.String(revoker.Name)));
			expr = SqlExpression.And(expr, granterCheck);

			t1 = t1.ExhaustiveSelect(context, expr);

			// Remove these rows from the table
			grantTable.Delete(t1);

			user.ClearGrantCache(objectName);
		}

		public static void GrantToUserOnSchema(this IQueryContext context, string schemaName, User user, Privileges privileges, bool withOption = false) {
			GrantToUserOnSchema(context, schemaName, user, context.User(), privileges, withOption);
		}

		public static void GrantToUserOnSchema(this IQueryContext context, string schemaName, User user, User granter, Privileges privileges, bool withOption = false) {
			context.GrantToUserOn(DbObjectType.Schema, new ObjectName(schemaName), user, granter, privileges, withOption);
		}

		public static Privileges GetUserGrants(this IQueryContext context, User user, DbObjectType objectType,
			ObjectName objectName, bool includePublic = false, bool onlyOption = false) {
			Privileges privileges;
			if (!user.TryGetObjectGrant(objectName, out privileges)) {
				privileges = context.GetUserGrants(user.Name, objectType, objectName);
				user.CacheObjectGrant(objectName, privileges);
			}

			return privileges;
		}

		public static Privileges GetUserGrants(this IQueryContext context, string userName, DbObjectType objType, ObjectName objName, bool includePublicPrivs = false, bool onlyGrantOptions = false) {
			// The system grants table.
			var grantTable = context.GetTable(SystemSchema.UserGrantsTableName);

			var objectCol = grantTable.GetResolvedColumnName(1);
			var paramCol = grantTable.GetResolvedColumnName(2);
			var granteeCol = grantTable.GetResolvedColumnName(3);
			var grantOptionCol = grantTable.GetResolvedColumnName(4);

			ITable t1 = grantTable;

			// All that match the given object parameter
			// It's most likely this will reduce the search by the most so we do
			// it first.
			t1 = t1.SimpleSelect(context, paramCol, BinaryOperator.Equal, SqlExpression.Constant(DataObject.String(objName.FullName)));

			// The next is a single exhaustive select through the remaining records.
			// It finds all grants that match either public or the grantee is the
			// username, and that match the object type.

			// Expression: ("grantee_col" = username OR "grantee_col" = 'public')
			var userCheck = SqlExpression.Equal(SqlExpression.Reference(granteeCol), SqlExpression.Constant(DataObject.String(userName)));
			if (includePublicPrivs) {
				userCheck = SqlExpression.Or(userCheck, SqlExpression.Equal(SqlExpression.Reference(granteeCol),
					SqlExpression.Constant(DataObject.String(User.PublicName))));
			}

			// Expression: ("object_col" = object AND
			//              ("grantee_col" = username OR "grantee_col" = 'public'))
			// All that match the given username or public and given object
			var expr = SqlExpression.And(SqlExpression.Equal(SqlExpression.Reference(objectCol),
				SqlExpression.Constant(DataObject.BigInt((int)objType))), userCheck);

			// Are we only searching for grant options?
			if (onlyGrantOptions) {
				var grantOptionCheck = SqlExpression.Equal(SqlExpression.Reference(grantOptionCol),
					SqlExpression.Constant(DataObject.BooleanTrue));
				expr = SqlExpression.And(expr, grantOptionCheck);
			}

			t1 = t1.ExhaustiveSelect(context, expr);

			// For each grant, merge with the resultant priv object
			Privileges privs = Privileges.None;

			foreach (var row in t1) {
				var priv = (int)row.GetValue(0).AsBigInt();
				privs |= (Privileges)priv;
			}

			return privs;
		}

		public static User CreateUser(this IQueryContext context, string userName, string password) {
			if (String.IsNullOrEmpty(userName))
				throw new ArgumentNullException("userName");
			if (String.IsNullOrEmpty(password))
				throw new ArgumentNullException("password");

			// TODO: make these rules configurable?

			if (userName.Length <= 1)
				throw new ArgumentException("User name must be at least one character.", "userName");
			if (password.Length <= 1)
				throw new ArgumentException("The password must be at least one character.", "password");

			if (String.Equals(userName, User.PublicName, StringComparison.OrdinalIgnoreCase))
				throw new ArgumentException(String.Format("User name '{0}' is reserved and cannot be registered.", User.PublicName), "userName");

			var c = userName[0];
			if (c == '#' || c == '@' || c == '$' || c == '&')
				throw new ArgumentException(String.Format("User name '{0}' is invalid: cannot start with '{1}' character.", userName, c), "userName");
			if (context.UserExists(userName))
				throw new DatabaseSystemException(String.Format("User '{0}' is already registered.", userName));

			// Add to the key 'user' table
			var table = context.GetMutableTable(SystemSchema.UserTableName);
			var row = table.NewRow();
			row[0] = DataObject.String(userName);
			table.AddRow(row);

			// TODO: get the hash algorithm and hash ...

			table = context.GetMutableTable(SystemSchema.PasswordTableName);
			row = table.NewRow();
			row.SetValue(0, userName);
			row.SetValue(1, 1);
			row.SetValue(2, password);
			table.AddRow(row);

			return new User(userName);
		}

		public static User Authenticate(this IQueryContext queryContext, string username, string password,
			ConnectionEndPoint endPoint) {
			try {
				var table = queryContext.GetTable(SystemSchema.PasswordTableName);
				var unameColumn = table.GetResolvedColumnName(0);
				var typeColumn = table.GetResolvedColumnName(1);
				var passwColumn = table.GetResolvedColumnName(2);
				var saltColumn = table.GetResolvedColumnName(3);
				var hashColumn = table.GetResolvedColumnName(4);

				var t = table.SimpleSelect(queryContext, unameColumn, BinaryOperator.Equal, SqlExpression.Constant(username));
				if (t.RowCount == 0)
					return null;

				var type = t.GetValue(0, typeColumn);
				if (type == 1) {
					// Clear-text password ...
					var pass = t.GetValue(0, passwColumn);
					if (pass == null || pass.Equals(DataObject.String(pass)))
						return null;

				} else if (type == 2) {
					// Hashed password ...
					var pass = t.GetValue(0, passwColumn);
					var salt = t.GetValue(0, saltColumn);
					var hash = t.GetValue(0, hashColumn);

					if (pass == null || salt == null || hash == null)
						return null;

					var crypto = PasswordCrypto.Parse(hash);
					if (!crypto.Verify(pass, password, salt))
						return null;
				} else if (type == 3) {
					// External authenticator ...
					// TODO:
				}

				// Now check if this user is permitted to connect from the given
				// host.
				if (!UserCanAccessFromHost(queryContext, username, endPoint))
					return null;

				// Successfully authenticated...
				return new User(username);
			} catch (Exception ex) {
				throw new DatabaseSystemException("Could not authenticate user.", ex);
			}
		}
	}
}