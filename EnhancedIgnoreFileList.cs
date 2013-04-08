//
// IgnoreFileList - Ignore defects based on a file descriptions
//
// Authors:
//	Sebastien Pouliot <sebastien@ximian.com>
//
// Copyright (C) 2008-2011 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

// copied from gendarme/console/IgnoreFileList.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;

using Mono.Cecil;
using Gendarme.Framework;
using Gendarme.Framework.Helpers;
using Gendarme.Framework.Rocks;

namespace Gendarme.MsBuild
{
	public class EnhancedIgnoreFileList : BasicIgnoreList
	{
		private class IgnoreFileInfo
		{
			public IgnoreFileInfo(string file, int lineNo)
			{
				File = file;
				LineNo = lineNo;
			}
			public string File { get; private set; }
			public int LineNo { get; private set; }
		}

		private string m_CurrentRule;
		private string m_CurrentFile;
		private int m_CurrentLine;
		private List<int> m_CurrentFileLineNumbers;
		private Dictionary<string, HashSet<string>> m_Assemblies = new Dictionary<string, HashSet<string>>();
		private Dictionary<string, HashSet<string>> m_Types = new Dictionary<string, HashSet<string>>();
		private Dictionary<string, HashSet<string>> m_Methods = new Dictionary<string, HashSet<string>>();
		private Dictionary<string, HashSet<string>> m_MethodsRegex = new Dictionary<string, HashSet<string>>();
		private Dictionary<string, Dictionary<string, IgnoreFileInfo>> m_IgnoredRules = new Dictionary<string, Dictionary<string, IgnoreFileInfo>>();
		private Stack<string> m_Files = new Stack<string>();
		private Dictionary<string, List<int>> m_FileLineNumbers = new Dictionary<string, List<int>>();
		private bool m_fAutoUpdateIgnores;

		public EnhancedIgnoreFileList(IRunner runner, string fileName, bool fAutoUpdateIgnores)
			: base (runner)
		{
			var initialIgnoreList = runner.IgnoreList as NotSupportedIgnoreList;
			if (initialIgnoreList != null)
			{
				// copy keys and values from existing ignore list
				foreach (var key in initialIgnoreList.Ignore.Keys)
				{
					foreach (var val in initialIgnoreList.Ignore[key])
						Add(key, val);
				}
			}

			m_fAutoUpdateIgnores = fAutoUpdateIgnores;
			Push(fileName);
			Parse();
		}

		private void Push(string fileName)
		{
			if (!String.IsNullOrEmpty(fileName) && File.Exists(fileName) && !m_Files.Contains(fileName))
			{
				m_Files.Push(fileName);
			}
		}

		private void Parse()
		{
			while (m_Files.Count > 0)
			{
				m_CurrentFile = m_Files.Pop();
				m_CurrentFileLineNumbers = new List<int>();
				using (StreamReader sr = new StreamReader(m_CurrentFile))
				{
					m_CurrentLine = 0;
					string s = sr.ReadLine();
					while (s != null)
					{
						m_CurrentLine++;
						ProcessLine(s);
						s = sr.ReadLine();
					}
				}
				m_FileLineNumbers.Add(m_CurrentFile, m_CurrentFileLineNumbers);
			}
			Resolve();
			TearDown();
		}

		private void Add(IDictionary<string, HashSet<string>> list, string rule, string target)
		{
			HashSet<string> rules;

			if (!list.TryGetValue(target, out rules))
			{
				rules = new HashSet<string>();
				list.Add(target, rules);
			}

			rules.Add(rule);

			if (!m_fAutoUpdateIgnores)
				return;

			Dictionary<string, IgnoreFileInfo> ignoredData;
			if (!m_IgnoredRules.TryGetValue(rule, out ignoredData))
			{
				ignoredData = new Dictionary<string, IgnoreFileInfo>();
				m_IgnoredRules.Add(rule, ignoredData);
			}
			ignoredData.Add(target, new IgnoreFileInfo(m_CurrentFile, m_CurrentLine));
			m_CurrentFileLineNumbers.Add(m_CurrentLine);
		}

		private void ProcessLine(string line)
		{
			if (line.Length < 1)
				return;

			switch (line[0])
			{
				case '#': // comment
					break;
				case 'R': // rule
					m_CurrentRule = line.Substring(line.LastIndexOf(' ') + 1);
					break;
				case 'A': // assembly - we support Name, FullName and *
					string target = line.Substring(2).Trim();
					if (target == "*")
					{
						foreach (AssemblyDefinition assembly in Runner.Assemblies)
						{
							Add(m_Assemblies, m_CurrentRule, assembly.Name.FullName);
						}
					}
					else
						Add(m_Assemblies, m_CurrentRule, target);
					break;
				case 'T': // type (no space allowed)
					Add(m_Types, m_CurrentRule, line.Substring(line.LastIndexOf(' ') + 1));
					break;
				case 'M': // method - we support full name and wildcard as part of method name.
					// This makes it easier to deal with anonymous methods which change part
					// of their name if the compiler changes the order of anonymous methods.
					var method = line.Substring(2).Trim();
					if (method.Contains("*"))
						Add(m_MethodsRegex, m_CurrentRule, method.Replace("*", "[A-Za-z0-9_]+"));
					else
						Add(m_Methods, m_CurrentRule, method);
					break;
				case 'N': // namespace - special case (no need to resolve)
					base.Add(m_CurrentRule, NamespaceDefinition.GetDefinition(line.Substring(2).Trim()));
					break;
				case '@': // include file
					m_Files.Push(line.Substring(2).Trim());
					break;
				default:
					Console.Error.WriteLine("Bad ignore entry : '{0}'", line);
					break;
			}
		}

		private void AddList(IMetadataTokenProvider metadata, IEnumerable<string> rules)
		{
			foreach (string rule in rules)
			{
				Add(rule, metadata);
			}
		}

		// scan the analyzed code a single time looking for targets
		private void Resolve()
		{
			HashSet<string> rule;
			var methodsRegex = new Dictionary<Regex, HashSet<string>>();

			foreach (var pattern in m_MethodsRegex.Keys)
			{
				methodsRegex.Add(new Regex(pattern), m_MethodsRegex[pattern]);
			}

			foreach (AssemblyDefinition assembly in Runner.Assemblies)
			{
				if (m_Assemblies.TryGetValue(assembly.Name.FullName, out rule))
				{
					AddList(assembly, rule);
				}
				if (m_Assemblies.TryGetValue(assembly.Name.Name, out rule))
				{
					AddList(assembly, rule);
				}

				foreach (ModuleDefinition module in assembly.Modules)
				{
					foreach (TypeDefinition type in module.GetAllTypes ())
					{
						if (m_Types.TryGetValue(type.FullName, out rule))
						{
							AddList(type, rule);
						}

						if (type.HasMethods)
						{
							foreach (MethodDefinition method in type.Methods)
							{
								// FIXME avoid (allocations in) ToString call
								var methodName = method.ToString();
								if (m_Methods.TryGetValue(methodName, out rule))
								{
									AddList(method, rule);
								}
								else
								{ // deal with wildcard method names
									foreach (Regex regex in methodsRegex.Keys)
									{
										if (regex.IsMatch(methodName))
											AddList(method, methodsRegex[regex]);
									}
								}
							}
						}
					}
				}
			}
		}

		private void TearDown()
		{
			m_Assemblies.Clear();
			m_Types.Clear();
			m_Methods.Clear();
		}

		/// <summary>
		/// Updates the file that lists ignored defects
		/// </summary>
		/// <param name="defects">Detected defects from a run without applying ignored list.</param>
		/// <remarks>Go through the file(s) with ignored defects. Anything listed that is no
		/// longer reported as a defect in <paramref name="defects"/> is commented out.</remarks>
		public void UpdateIgnores(Collection<Defect> defects)
		{
			UpdateIgnoreList(defects);

			// The remaining line numbers in m_FileLineNumbers are those that didn't match
			// a defect
			foreach (var kvpair in m_FileLineNumbers)
			{
				var fileName = kvpair.Key;
				UpdateIgnoreFile(fileName, kvpair.Value);
			}
		}

		protected void UpdateIgnoreList(Collection<Defect> defects)
		{
			foreach (var defect in defects)
			{
				if (IsIgnored(defect.Rule, defect.Location) ||
					IsIgnored(defect.Rule, defect.Target))
				{
					Dictionary<string, IgnoreFileInfo> ignoredData;
					if (m_IgnoredRules.TryGetValue(defect.Rule.FullName, out ignoredData))
					{
						IgnoreFileInfo ignoreFileInfo;
						var member = defect.Target as MemberReference;
						if (ignoredData.TryGetValue(member.FullName, out ignoreFileInfo))
						{
							var lineNumbers = m_FileLineNumbers[ignoreFileInfo.File];
							lineNumbers.Remove(ignoreFileInfo.LineNo);
						}
					}
				}
			}
		}

		protected void UpdateIgnoreFile(string fileName, List<int> ignoreLineNumbers)
		{
			if (ignoreLineNumbers.Count == 0)
				return;

			ignoreLineNumbers.Sort();
			var tmpFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(fileName));
			try
			{
				File.Copy(fileName, tmpFile);
				using (StreamReader sr = new StreamReader(tmpFile))
				{
					using (StreamWriter sw = new StreamWriter(fileName))
					{
						int lineNo = 0;
						for (string s = sr.ReadLine(); s != null; s = sr.ReadLine())
						{
							lineNo++;
							if (ignoreLineNumbers.Contains(lineNo))
								sw.Write("##Commented by AutoUpdateIgnore## ");
							sw.WriteLine(s);
						}
					}
				}
			}
			finally
			{
				File.Delete(tmpFile);
			}
		}
	}
}
