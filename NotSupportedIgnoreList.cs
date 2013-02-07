// --------------------------------------------------------------------------------------------
// <copyright from='2012' to='2012' company='SIL International'>
// 	Copyright (c) 2012, SIL International. All Rights Reserved.
//
// 	Distributable under the terms of either the Common Public License or the
// 	GNU Lesser General Public License, as specified in the LICENSING.txt file.
// </copyright>
// --------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Mono.Cecil;
using Gendarme.Framework;

namespace Gendarme.MsBuild
{
	/// <summary>
	/// A do-nothing implementation of an ignore list. It's sole purpose is to store the
	/// ignore rules we get (from attributes) so that we can use them later on.
	/// </summary>
	internal class NotSupportedIgnoreList: IIgnoreList
	{
		private readonly Dictionary<string, HashSet<IMetadataTokenProvider>> m_Ignore =
			new Dictionary<string, HashSet<IMetadataTokenProvider>>();

		internal Dictionary<string, HashSet<IMetadataTokenProvider>> Ignore
		{
			get { return m_Ignore; }
		}

		#region IIgnoreList implementation
		public void Add(string rule, IMetadataTokenProvider metadata)
		{
			HashSet<IMetadataTokenProvider> list;
			if (!m_Ignore.TryGetValue(rule, out list))
			{
				list = new HashSet<IMetadataTokenProvider>();
				m_Ignore.Add(rule, list);
			}
			list.Add(metadata);
		}

		public bool IsIgnored(IRule rule, IMetadataTokenProvider metadata)
		{
			return false;
		}

		public IRunner Runner
		{
			get
			{
				return null;
			}
		}
		#endregion
	}
}

