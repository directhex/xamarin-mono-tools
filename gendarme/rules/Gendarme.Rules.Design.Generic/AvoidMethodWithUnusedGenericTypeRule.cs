//
// Gendarme.Rules.Design.Generic.AvoidMethodWithUnusedGenericTypeRule
//
// Authors:
//	Sebastien Pouliot <sebastien@ximian.com>
//
// Copyright (C) 2008 Novell, Inc (http://www.novell.com)
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

using Mono.Cecil;

using Gendarme.Framework;

namespace Gendarme.Rules.Design.Generic {

	/// <summary>
	/// This method will fire if a generic method does not use all of its generic type parameters
	/// in the formal parameter list. This usually means that either the type parameter is not used at
	/// all in which case it should be removed or that it's used only for the return type which
	/// is problematic because that prevents the compiler from inferring the generic type 
	/// when the method is called which is confusing to many developers.
	/// </summary>
	/// <example>
	/// Bad example:
	/// <code>
	/// public class Bad {
	///	public string ToString&lt;T&gt; ()
	///	{
	///		return typeof (T).ToString ();
	///	}
	///	
	///	static void Main ()
	///	{
	///		// the compiler can't infer int so we need to supply it ourselves
	///		Console.WriteLine (ToString&lt;int&gt; ());
	///	}
	/// }
	/// </code>
	/// </example>
	/// <example>
	/// Good example:
	/// <code>
	/// public class Good {
	///	public string ToString&lt;T&gt; (T obj)
	///	{
	///		return obj.GetType ().ToString ();
	///	}
	///	
	///	static void Main ()
	///	{
	///		Console.WriteLine (ToString (2));
	///	}
	/// }
	/// </code>
	/// </example>
	/// <remarks>This rule is available since Gendarme 2.2</remarks>

	[Problem ("One or more generic type parameters are not used in the formal parameter list.")]
	[Solution ("This prevents the compiler from inferring types when the method is used which results in hard to use API definitions.")]
	[FxCopCompatibility ("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter")]
	public class AvoidMethodWithUnusedGenericTypeRule : Rule, IMethodRule {

		public override void Initialize (IRunner runner)
		{
			base.Initialize (runner);

			// we only want to run this on assemblies that use 2.0 or later
			// since generics were not available before
			Runner.AnalyzeAssembly += delegate (object o, RunnerEventArgs e) {
				Active = (e.CurrentAssembly.Runtime >= TargetRuntime.NET_2_0);
			};
		}

		static bool FindGenericType (GenericInstanceType git, string fullname)
		{
			foreach (object o in git.GenericArguments) {
				GenericParameter igp = (o as GenericParameter);
				if (igp != null) {
					if (igp.FullName == fullname)
						return true;
					continue;
				}

				GenericInstanceType inner = (o as GenericInstanceType);
				if ((inner != null) && (FindGenericType (inner, fullname)))
					return true;
			}
			return false;
		}

		public RuleResult CheckMethod (MethodDefinition method)
		{
			// rule applies only if the method has generic type parameters
			if (!method.HasGenericParameters)
				return RuleResult.DoesNotApply;

			// look if every generic type parameter...
			foreach (GenericParameter gp in method.GenericParameters) {
				bool found = false;
				// ... is being used by the method parameters
				foreach (ParameterDefinition pd in method.Parameters) {
					if (pd.ParameterType.FullName == gp.FullName) {
						found = true;
						break;
					}

					// handle things like ICollection<T>
					GenericInstanceType git = (pd.ParameterType as GenericInstanceType);
					if (git == null)
						continue;

					if (FindGenericType (git, gp.FullName)) {
						found = true;
						break;
					}
				}
				if (!found) {
					string msg = String.Format ("Generic parameter '{0}' is not used by the method parameters.", gp.FullName);
					Runner.Report (method, Severity.Medium, Confidence.High, msg);
				}
			}
			return Runner.CurrentRuleResult;
		}
	}
}
