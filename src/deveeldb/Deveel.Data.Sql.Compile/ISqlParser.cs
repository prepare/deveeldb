﻿using System;
using System.IO;
using System.Text;

namespace Deveel.Data.Sql.Compile {
	/// <summary>
	/// Implementations of this interface will parse input
	/// strings into <see cref="ISqlNode"/> that can
	/// be used to construct objects for interacting with 
	/// the system.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Parsers are specific for given SQL dialects (eg. <c>SQL-92</c>,
	/// <c>SQL-99</c>, <c>PL/SQL-1</c>, etc.): the results of the parse
	/// operation must be analyzed based on this factor.
	/// </para>
	/// </remarks>
	/// <seealso cref="ISqlNode"/>
	/// <seealso cref="ISqlNodeVisitor"/>
	/// <seealso cref="SqlParseResult"/>
	public interface ISqlParser : IDisposable {
		/// <summary>
		/// Gets the string identifying the SQL dialect used to parse
		/// </summary>
		/// <remarks>
		/// <para>
		/// A dialect name must be unique within a system and it
		/// is used to analyze the results of parses.
		/// </para>
		/// <para>
		/// The results will include this value as constructor
		/// parameter.
		/// </para>
		/// </remarks>
		/// <seealso cref="SqlParseResult.Dialect"/>
		string Dialect { get; }

		/// <summary>
		/// Analyzes and parses the input and results an object
		/// that describes the parsed nodes in a tree that can
		/// be used to construct objects for interacting with
		/// the system.
		/// </summary>
		/// <param name="input">The input string to be parsed.</param>
		/// <returns>
		/// Returns an instance of <see cref="SqlParseResult"/> that
		/// includes information about the analyzed input.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// If the provided <paramref name="input"/> is <c>null</c> or empty.
		/// </exception>
		/// <seealso cref="SqlParseResult"/>
		SqlParseResult Parse(string input);
	}
}
