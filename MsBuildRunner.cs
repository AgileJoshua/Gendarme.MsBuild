//
// Gendarme.NAnt.NAntRunner class
//
// Authors:
//	Néstor Salceda <nestor.salceda@gmail.com>
//
// 	(C) 2008 Néstor Salceda
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

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Gendarme.Framework;
using Gendarme.Framework.Engines;
using Mono.Cecil;

namespace Gendarme.MsBuild
{
	/// <summary>
	/// Gendarme runner for msbuild.
	/// </summary>
	[EngineDependency(typeof (SuppressMessageEngine))]
	internal class MsBuildRunner : Runner
	{
		private string IgnoreFile { get; set; }
		private IIgnoreList OriginalIgnoreList { get; set; }
		private TaskLoggingHelper Log { get; set; }
		private LogTypeEnum LogType { get; set; }
		private string LogFile { get; set;}

		public MsBuildRunner(TaskLoggingHelper log, LogTypeEnum logType, string logFile) : base()
		{
			Log = log;
			LogType = logType;
			LogFile = logFile;
			OriginalIgnoreList = IgnoreList = new NotSupportedIgnoreList();
		}

		public bool AutoUpdateIgnore { get; set; }

		public void AddIgnoreFile(string ignoreFile)
		{
			IgnoreFile = ignoreFile;
			if (!AutoUpdateIgnore)
				IgnoreList = new EnhancedIgnoreFileList(this, ignoreFile, AutoUpdateIgnore);
		}

		public override void Run()
		{
			if (AutoUpdateIgnore && !string.IsNullOrEmpty(IgnoreFile))
			{
				// First run without ignore file so that we can discover unnecessary lines in
				// ignore file
				Log.LogMessage(MessageImportance.Normal, "AutoUpdateIgnore run");
				base.Run();

				var ignoreList = new EnhancedIgnoreFileList(this, IgnoreFile, AutoUpdateIgnore);
				ignoreList.UpdateIgnores(Defects);

				// now do it for real
				Defects.Clear();
				IgnoreList = OriginalIgnoreList;
				IgnoreList = new EnhancedIgnoreFileList(this, IgnoreFile, false);
			}

			Log.LogMessage(MessageImportance.Low, "Starting Gendarme Code Analyzer");
			base.Run();
		}

		public void PrintInScreen()
		{
			int index = 0;
			
			foreach (Defect defect in Defects)
			{
				IRule rule = defect.Rule;
				
				Log.LogMessage(MessageImportance.Low, "{0}. {1}", ++index, rule.Name);
				Log.LogMessage(MessageImportance.Low, "\n");
				Log.LogMessage(MessageImportance.Low, "Problem: {0}", rule.Problem);
				Log.LogMessage(MessageImportance.Low, "\n");
				Log.LogMessage(MessageImportance.Low, "Details [Severity: {0}, Confidence: {1}]", defect.Severity, defect.Confidence);
				Log.LogMessage(MessageImportance.Low, "* Target: {0}", defect.Target);
				Log.LogMessage(MessageImportance.Low, "* Location: {0}", defect.Location);
				if (!String.IsNullOrEmpty(defect.Text))
					Log.LogMessage(MessageImportance.Low, "* {0}", defect.Text);
				Log.LogMessage(MessageImportance.Low, "\n");
				Log.LogMessage(MessageImportance.Low, "Solution: {0}", rule.Solution);
				Log.LogMessage(MessageImportance.Low, "\n");
				Log.LogMessage(MessageImportance.Low, "More info available at: {0}", rule.Uri.ToString());
				Log.LogMessage(MessageImportance.Low, "\n");
			}
		}

		public void WriteReport()
		{
			if (Defects.Count == 0)
				Log.LogMessage(MessageImportance.Normal, "No errors detected");
			else
			{
				ResultWriter resultWriter = null;
				
				switch (LogType)
				{
				case LogTypeEnum.None:
					PrintInScreen();
					break;
				case LogTypeEnum.Plain:
					resultWriter = new TextResultWriter(this, LogFile);
					break;
				case LogTypeEnum.Xml:
					resultWriter = new XmlResultWriter(this, LogFile);
					break;
				case LogTypeEnum.Html:
					resultWriter = new HtmlResultWriter(this, LogFile);
					break;
				default:
					break;
				}
				
				if (resultWriter != null)
				{
					resultWriter.Report();
					resultWriter.Dispose();
				}
			}
		}
		
	}
}
