// --------------------------------------------------------------------------------------------
// <copyright from='2013' to='2013' company='SIL International'>
// 	Copyright (c) 2013, SIL International. All Rights Reserved.
//
// 	Distributable under the terms of either the Common Public License or the
// 	GNU Lesser General Public License, as specified in the LICENSING.txt file.
// </copyright>
// --------------------------------------------------------------------------------------------
using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Gendarme.Framework;
using Mono.Cecil;

namespace Gendarme.MsBuild
{
	public enum LogTypeEnum
	{
		None,
		Plain,
		Xml,
		Html
	}

	/// <summary>
	/// Gendarme task for msbuild/xbuild.
	/// </summary>
	/// <remarks>This class is based on the GendarmeTask class for NAnt of
	/// Néstor Salceda (nestor.salceda@gmail.com) </remarks>
	public class Gendarme: Task
	{
		[Required]
		public string ConfigurationFile { get; set; }

		[Required]
		public string RuleSet { get; set; }

		public string IgnoreFile { get; set; }

		[Required]
		public string Assembly { get; set; }

		private LogTypeEnum m_LogType = LogTypeEnum.None;

		public string LogType
		{
			get
			{
				return m_LogType.ToString();
			}
			set
			{
				m_LogType = (LogTypeEnum)Enum.Parse(typeof(LogTypeEnum), value);
			}
		}

		public string LogFile { get; set; }

		public bool AutoUpdateIgnores { get; set; }

		public bool VerifyFail { get; set; }

		private void CheckDependencies()
		{
			if (m_LogType == LogTypeEnum.None && !string.IsNullOrEmpty(LogFile))
				Log.LogError("You shouldn't put a file and None in the LogType attribute.");
			if (m_LogType != LogTypeEnum.None && string.IsNullOrEmpty(LogFile))
				Log.LogError("You have to specify a file which will contain the log.");
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Executes the task.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public override bool Execute()
		{
			Log.LogMessage(MessageImportance.Normal, "Gendarmalyzing {0}", Assembly);
			CheckDependencies();
			var runner = new MsBuildRunner(Log, m_LogType, LogFile);
			var config = new Settings(runner, ConfigurationFile, RuleSet);
			config.Load();
			var assemblyDef = AssemblyDefinition.ReadAssembly(Assembly,
				new ReaderParameters { AssemblyResolver = AssemblyResolver.Resolver });
			runner.Assemblies.Add(assemblyDef);
			runner.AutoUpdateIgnore = AutoUpdateIgnores;
			runner.AddIgnoreFile(IgnoreFile);
			runner.Initialize();
			runner.Run();
			runner.WriteReport();
			if (m_LogType == LogTypeEnum.None || (m_LogType == LogTypeEnum.Plain && string.IsNullOrEmpty(LogFile)))
				runner.PrintInScreen();
			if (runner.Defects.Count > 0)
			{
				string message;
				if (m_LogType == LogTypeEnum.None || (m_LogType == LogTypeEnum.Plain && string.IsNullOrEmpty(LogFile)))
					message = string.Format("Gendarme found {0} defects in code", runner.Defects.Count);
				else
				{
					message = string.Format("Gendarme found {0} defects in code. See {1} for details.",
						runner.Defects.Count, LogFile);
				}
				Log.LogError(message);
			}
			return !(Log.HasLoggedErrors && VerifyFail);
		}
	}
}

