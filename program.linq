<Query Kind="Program" />

void Main()
{
	/* 
		content of d:\pdf should be:
		AdobeRGB1998.icc
		gsdll32.dll
		gswin32c.exe
		PDFA_def.ps
		PDF_ShowBookmarksPanel.ps
		wkhtmltopdf.exe
		content\1.css
		content\1.html
	*/
	var options = new PdfGenerationOptions { 
		GhostscriptExe = @"d:\pdf\gswin32c.exe",
		OutputDirectory = @"d:\pdf\content\\",
		PdfUtilitiesPath = @"d:\pdfcontent\\",
		WkhtmlToPdfExe = @"d:\pdf\wkhtmltopdf.exe"
	};
	GeneratePDF(options, @"d:\pdf\1.html", @"d:\pdf\1.css" );
}

public class PdfGenerationOptions
{
	// Should point to a directory where we have: PDFA_def.ps, PDF_ShowBookmarksPanel.ps, AdobeRGB1998.icc
	public string PdfUtilitiesPath { get; set; }
	// Should point to a directory where we have wkhtmltopdf.exe
	public string WkhtmlToPdfExe { get; set; }
	// Should point to a directory where we have: gswin32c.exe or gswin64c.exe, gsdll32.dll or gsdll64.dll
	public string GhostscriptExe { get; set; }
	public string OutputDirectory { get; set; }
}

public void GeneratePDF(PdfGenerationOptions options, string htmlFile, string cssFile = null)
{
	string pdfName = "pdf.pdf";
	string pdfaName = "pdfa.pdf";
	Tuple<int, List<string>> runResults = null;

	try
	{
		// set up
		var finalPdfPath = Path.Combine(options.OutputDirectory, pdfName);
		var pdfaSettingsPath = Path.Combine(options.PdfUtilitiesPath, "PDFA_def.ps");
		var finalPdfAPath = Path.Combine(options.OutputDirectory, pdfaName);

		// ignores resources loading errors, such as missing CSS files
		// wkhtmltopdf.exe --help for more options
		var wKhtmlIgnoreErrors = "--load-error-handling ignore";
		var wKhtmlCss = $@"--user-style-sheet {cssFile}";
		var wKhtmlOptions = new List<string> { htmlFile, wKhtmlIgnoreErrors, wKhtmlCss, finalPdfPath };

		var ghostscriptOptions = new string[] { "-dPDFA", "-dBATCH", "-dNOPAUSE", "-dNOOUTERSAVE",
					"-dNumRenderingThreads=2", "-dMaxPatternBitmap=200000", "-dNOGC", "-dBandBufferSpace=500000000",
					"-sBandListStorage=memory", "-dBufferSpace=1000000000", "-dCompressFonts=true", "-dDetectDuplicateImages=true",
					"-sProcessColorModel=DeviceRGB", "-sDEVICE=pdfwrite",
					$"-sOutputFile=\"{finalPdfAPath}\"",
					"-dPDFACompatibilityPolicy=1",
					$"\"{pdfaSettingsPath}\"",
					$"\"{finalPdfPath}\"" };

		// run wkhtmltopdf
		runResults = RunProcess(options.WkhtmlToPdfExe, wKhtmlOptions.ToArray(), options.OutputDirectory);

		// run ghostscript
		runResults = RunProcess(options.GhostscriptExe, ghostscriptOptions.ToArray(), Path.GetDirectoryName(options.GhostscriptExe));
	}
	finally
	{
		// if just temporary folder, delete when exiting
		//if (Directory.Exists(options.OutputDirectory)
		//{
		//	Directory.Delete(options.OutputDirectory);
		//}
	}
}

private Tuple<int, List<string>> RunProcess(string command, string[] options, string workingDir = null)
{
	int result = -1;
	List<string> messages = new List<string>();
	try
	{
		using (Process process = new Process())
		{
			process.StartInfo.FileName = command;
			process.StartInfo.Arguments = string.Join(" ", options);
			if (!string.IsNullOrWhiteSpace(workingDir))
			{
				process.StartInfo.WorkingDirectory = workingDir;
			}
			process.StartInfo.CreateNoWindow = true;
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.RedirectStandardError = true;
			Func<bool> read = () =>
			{
				Action<string, string> logMessage = (stream, msg) => messages.Add($"{stream}: {msg}");
				string s = process.StandardOutput.ReadLine();
				if (s == null)
				{
					if ((s = process.StandardError.ReadLine()) == null) return false;
					logMessage("stderr", s);
				}
				else
				{
					logMessage("stdout: ", s);
				}
				return true;
			};
			process.Start();
			while (read()) { }
			process.WaitForExit(1800 * 1000); // 30 min

			int returnCode = process.ExitCode;
			process.Close();
		}
	}
	catch (Exception exc)
	{
		throw new Exception("Exception when running " + command, exc);
	}

	return new Tuple<int, List<string>>(result, messages);
}