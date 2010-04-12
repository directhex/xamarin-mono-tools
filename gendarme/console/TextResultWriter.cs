//
// TextResultWriter
//
// Authors:
//	Christian Birkl <christian.birkl@gmail.com>
//	Sebastien Pouliot <sebastien@ximian.com>
//
// Copyright (C) 2006 Christian Birkl
// Copyright (C) 2006, 2008 Novell, Inc (http://www.novell.com)
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
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mono.Cecil;

using Gendarme.Framework;

namespace Gendarme {

	public class TextResultWriter : ResultWriter, IDisposable {

		[Serializable]
		enum ColorScheme {
			None,
			Light,
			Dark
		}

		private TextWriter writer;
		private bool need_closing;
		private ColorScheme color_scheme;
		private int index;

		public TextResultWriter (IRunner runner, string fileName)
			: base (runner, fileName)
		{
			if (String.IsNullOrEmpty (fileName)) {
				writer = System.Console.Out;

				string color_override = Environment.GetEnvironmentVariable ("GENDARME_COLOR") ?? "dark";
				switch (color_override.ToLowerInvariant ()) {
				case "none":
					break;
				case "light":
					color_scheme = ColorScheme.Light;
					break;
				case "dark":
				default:
					color_scheme = ColorScheme.Dark;
					break;
				}
			} else {
				writer = new StreamWriter (fileName);
				need_closing = true;
			}
		}

		protected override void Write ()
		{
			// casting to Severity int saves a ton of memory since IComparable<T> can be used instead of IComparable
			var query = from n in Runner.Defects
				    orderby (int) n.Severity, n.Rule.Name
				    select n;
			
			WriteHeader ();
			if (query.Any ()) {				
				string name = string.Empty;
				string delimiter = new string ('-', 60);
				foreach (Defect defect in query) {
					if (defect.Rule.Name != name) {
						writer.WriteLine (delimiter);
						name = defect.Rule.Name;
					}
					
					WriteDefect (defect);
				}
			}
			WriteTrailer (query.Count ());
		}
		
		private void WriteHeader ()
		{
			writer.WriteLine ("Produced on {0} for:", DateTime.UtcNow);
			foreach (AssemblyDefinition assembly in Runner.Assemblies)
				writer.WriteLine ("   {0}", assembly.Name.Name);
			writer.WriteLine ();
		}

		private void WriteDefect (Defect defect)
		{
			IRule rule = defect.Rule;

			BeginColor (
				(Severity.Critical == defect.Severity || Severity.High == defect.Severity)
				? ConsoleColor.DarkRed : ConsoleColor.DarkYellow);
			writer.WriteLine ("{0}. {1}", ++index, rule.Name);
			writer.WriteLine ();
			EndColor ();

			BeginColor (ConsoleColor.DarkRed);
			writer.Write ("Problem: ");
			EndColor ();
			writer.Write (rule.Problem);
			writer.WriteLine ();

			writer.WriteLine ("* Severity: {0}, Confidence: {1}", defect.Severity, defect.Confidence);
			writer.WriteLine ("* Target:   {0}", defect.Target);
			if (defect.Location != defect.Target)
				writer.WriteLine ("* Location: {0}", defect.Location);				
			if (!String.IsNullOrEmpty (defect.Source))
				writer.WriteLine ("* Source:   {0}", defect.Source);
			if (!String.IsNullOrEmpty (defect.Text))
				writer.WriteLine ("* Details:  {0}", defect.Text);
			writer.WriteLine ();

			BeginColor (ConsoleColor.DarkGreen);
			writer.Write ("Solution: ");
			EndColor ();
			writer.Write (rule.Solution);
			writer.WriteLine ();

			writer.WriteLine ("More info available at: {0}", rule.Uri.ToString ());
			writer.WriteLine ();
			writer.WriteLine ();
		}

		private void WriteTrailer (int numDefects)
		{
			if (numDefects == 0)
				writer.WriteLine ("Processed {0} rules, but found no defects.", Runner.Rules.Count);
			else if (Runner.Rules.Count == 1)
				writer.WriteLine ("Processed one rule.");	// we don't print the number of defects here because it is listed with the defect
			else
				writer.WriteLine ("Processed {0} rules.", Runner.Rules.Count);
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing) {
				if (need_closing) {
					writer.Dispose ();
				}
			}
		}

		private void BeginColor (ConsoleColor color)
		{
			switch (color_scheme) {
			case ColorScheme.Dark:
				Console.ForegroundColor = color;
				break;
			case ColorScheme.Light:
				Console.ForegroundColor = (ConsoleColor) color + 8;
				break;
			}
		}

		private void EndColor ()
		{
			switch (color_scheme) {
			case ColorScheme.Dark:
			case ColorScheme.Light:
				Console.ResetColor ();
				break;
			}
		}
	}
}
