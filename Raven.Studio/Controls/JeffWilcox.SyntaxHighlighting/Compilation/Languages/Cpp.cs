﻿// Based on Jeff Wilcox' Syntax highlighter: http://www.jeff.wilcox.name/2010/03/syntax-highlighting-text-block/
// He really deservces most of the credits...

// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.
// <auto-generated />
// No style analysis for imported project.

namespace Raven.Studio.Controls.JeffWilcox.SyntaxHighlighting
{
	using System.Collections.Generic;

	class Cpp : ILanguage
	{
		public string Id
		{
			get { return LanguageId.Cpp; }
		}

		public string Name
		{
			get { return "C++"; }
		}

		public string FirstLinePattern
		{
			get { return null; }
		}

		public IList<LanguageRule> Rules
		{
			get
			{
				return new List<LanguageRule>
				       	{
				       		new LanguageRule(
				       			@"/\*([^*]|[\r\n]|(\*+([^*/]|[\r\n])))*\*+/",
				       			new Dictionary<int, string>
				       				{
				       					{0, ScopeName.Comment},
				       				}),
				       		new LanguageRule(
				       			@"(//.*?)\r?$",
				       			new Dictionary<int, string>
				       				{
				       					{1, ScopeName.Comment}
				       				}),
				       		new LanguageRule(
				       			@"(?s)(""[^\n]*?(?<!\\)"")",
				       			new Dictionary<int, string>
				       				{
				       					{0, ScopeName.String}
				       				}),
				       		new LanguageRule(
				       			@"\b(auto|bool|break|case|catch|char|class|const|const_cast|continue|default|delete|do|double|dynamic_cast|else|enum|explicit|export|extern|false|float|for|friend|goto|if|inline|int|long|mutable|namespace|new|operator|private|protected|public|register|reinterpret_cast|return|short|signed|sizeof|static|static_cast|struct|switch|template|this|throw|true|try|typedef|typeid|typename|union|unsigned|using|virtual|void|volatile|wchar_t|while)\b",
				       			new Dictionary<int, string>
				       				{
				       					{0, ScopeName.Keyword},
				       				}),
				       	};
			}
		}
	}
}