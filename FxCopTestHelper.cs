// -----------------------------------------------------------------------
// Simple helper to unit-test fxcop rules
// -----------------------------------------------------------------------

namespace KlugeSoftware.FxCop.UnitTests {
	using System.IO;
	using System.Linq;
	using System.Reflection;
	using Microsoft.FxCop.Sdk;
	using Microsoft.VisualStudio.TestTools.UnitTesting;

	// all test-assemblies should be referenced, so they recompile on test execution (if needed)
	// and can be located in same path as current executing assembly
	// There have to be a pdb-file with full debug info so the 'sourcefile' and 'sourceline'
	// properties are filled and can be used for testing
	static class FxCopTestHelper {
		private static string _testPath;
		private static object _lock = new object( );

		public static string TestPath {
			get {
				if( _testPath == null ) {
					lock( _lock ) {
						if( _testPath == null ) {
							_testPath = Path.GetDirectoryName( Assembly.GetExecutingAssembly( ).Location );
						}
					}
				}
				return _testPath;
			}
		}

		public static AssemblyNode LoadAssembly( string assemblyName ) {
			return LoadAssembly( FxCopTestHelper.TestPath, assemblyName );
		}

		public static AssemblyNode LoadAssembly( string path, string assemblyName ) {
			var ret = AssemblyNode.GetAssembly( Path.Combine( path, assemblyName ), null, true, true, false, true );
			if( ret == null ) {
				Assert.Fail( "Cannot load assembly {0} in directory {1}", assemblyName, path );
			}
			if( !ret.HasDebugSymbols ) {
				Assert.Fail( "Cannot load Debug symbols of assembly {0} in directory {1}", assemblyName, path );
			}
			return ret;
		}


		public static void ApplyTests( this ProblemCollection problems, TestContext context, params Test[] problemTests ) {
			if( problemTests.Length == 0 ) {
				Assert.IsFalse( problems == null || problems.Count == 0, "Problems reported that are not tested" );
			} else {
				Assert.IsFalse( problems == null || problems.Count == 0, "No problems reported, but there should be {0}", problemTests.Length );

				var unusedTests = problemTests.AsEnumerable( );
				var untested = problems.Where(
					p => {
						var used = unusedTests.Where( pt => pt.IsSame( p ) );
						unusedTests = unusedTests.Where( pt => !used.Contains( pt ) );
						return used.Count( ) == 0;
					}
				);

				bool hasUntested = untested.Count( ) > 0;
				bool hasUnused = unusedTests.Count( ) > 0;

				if( hasUntested ) {
					if( context != null ) {
						context.WriteLine( "Untested problems" );
						foreach( var untestedItem in untested ) {
							if( untestedItem.SourceFile != null ) context.WriteLine( "    File: {0}", untestedItem.SourceFile );
							if( untestedItem.SourceLine != 0 ) context.WriteLine( "    Line: {0}", untestedItem.SourceLine );
							if( untestedItem.Resolution != null ) {
								var items = untestedItem.Resolution.Items;
								if( items != null && items.Count > 0 ) {
									context.WriteLine( "    Data: {0}", items[0] );
									for( int i = 1; i < items.Count; i++ ) {
										context.WriteLine( "              {0}", items[i] );
									}
								}
							}
							context.WriteLine( "" );
						}
					}

				}

				if( hasUnused ) {

					if( context != null ) {
						context.WriteLine( "Unraised problems" );
						foreach( var unusedTest in unusedTests ) {

							if( unusedTest.File != null ) context.WriteLine( "    File: {0}", unusedTest.File );
							if( unusedTest.Line.HasValue ) context.WriteLine( "    Line: {0}", unusedTest.Line.Value );
							if( unusedTest.Items != null && unusedTest.Items.Length > 0 ) {
								context.WriteLine( "    Data: {0}", unusedTest.Items[0] );
								for( int i = 1; i < unusedTest.Items.Length; i++ ) {
									context.WriteLine( "              {0}", unusedTest.Items[i] );
								}

							}
							context.WriteLine( "" );
						}
					}
				}

				// fails at the end, so all information are written into test results details
				if( hasUntested ) {
					Assert.Fail( "More problems reported than tests defined.{0}",
						context != null ? "See test results details for more information" : "" );
				}
				if( hasUnused ) {
					Assert.Fail( "More tests defined than problems reported.{0}",
						context != null ? "See test results details for more information" : "" );
				}
			}
		}
	}


	sealed class Test {
		public string File { get; set; }
		public int? Line { get; set; }
		public object[] Items { get; set; }

		public bool IsSame( Problem problem ) {

			if( File == null || problem.SourceFile == null || problem.SourceFile.EndsWith( "\\" + File, System.StringComparison.OrdinalIgnoreCase ) ) {
				if( !Line.HasValue || problem.SourceLine == 0 || Line.Value == problem.SourceLine ) {
					var res = problem.Resolution;
					if(
						Items == null || res == null || res.Items == null ||
						(
							res.Items.Count == Items.Length &&
							Items.Where( ( p, i ) => !object.Equals( res.Items[i], p ) ).Count( ) == 0
						)
					) {
						return true;
					}
				}
			}
			return false;
		}
	}
}