﻿/**
 *  Part of the Diagnostics Kit
 *
 *  Copyright (C) 2016  Sebastian Solnica
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.

 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 */

using Fiddler;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Windows.Forms;

[assembly: RequiredVersion("4.4.4.0")]

namespace LowLevelDesign.Diagnostics.BishopUpdateShim
{
    public class FiddlerPlugin : IAutoTamper
    {
        private const int DefaultRequestTimeoutInMilliseconds = 1500;
        private const string BishopDllName = "_Bishop.dll";
        private readonly string configurationFilePath = Path.Combine(Path.GetDirectoryName(
            Assembly.GetExecutingAssembly().Location), "bishop.conf");
        private IAutoTamper bishop;

        public FiddlerPlugin()
        {
            try
            {
                Version installedVer = new Version(0, 0, 0, 0);
                Version currentVer;
                string updateFileHash, updateFileUrl;

                // try loading the Castle url
                var diagnosticsCastleUrl = ExtractDiagnosticsCastleUrl();
                if (diagnosticsCastleUrl == null) {
                    return;
                }
                var req = WebRequest.Create(diagnosticsCastleUrl + "/about-bishop");
                req.Timeout = DefaultRequestTimeoutInMilliseconds;
                using (var s = new StreamReader(req.GetResponse().GetResponseStream()))
                {
                    var updateInfo = s.ReadToEnd().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (updateInfo.Length != 3) {
                        return;
                    }
                    currentVer = new Version(updateInfo[0]);
                    updateFileHash = updateInfo[1];
                    updateFileUrl = updateInfo[2];
                }

                var fiddlerUserPath = CONFIG.GetPath("AutoFiddlers_User");
                var asmpath = Path.Combine(fiddlerUserPath, BishopDllName);
                if (File.Exists(asmpath))
                {
                    installedVer = AssemblyName.GetAssemblyName(asmpath).Version;
                }

                if (currentVer > installedVer)
                {
                    if (MessageBox.Show(string.Format("New Bishop version available: {0}. Update?", currentVer), "Bishop update",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        var tempPath = Path.Combine(Path.GetTempPath(), "Bishop.zip");
                        using (var wc = new WebClient())
                        {
                            wc.DownloadFile(updateFileUrl, tempPath);
                            if (!IsFileUnaltered(tempPath, updateFileHash))
                            {
                                File.Delete(tempPath);
                                throw new InvalidOperationException(
                                    "There was a problem in the update download - the hash does not match.");
                            }
                        }
                        FileUtils.ExtractZipToDirectoryAndOverrideExistingFiles(tempPath, fiddlerUserPath);
                        File.Delete(tempPath);
                    }
                }
            }
            catch (Exception ex)
            {
                // this is cruel but what can we do? :)
                Trace.Write("Error when trying to check the update version", "Exception: " + ex);
            }
        }

        private string ExtractDiagnosticsCastleUrl()
        {
            if (!File.Exists(configurationFilePath)) {
                return null;
            }
            string content = File.ReadAllText(configurationFilePath);
            var startind = content.IndexOf("\"DiagnosticsUrl\"");
            if (startind < 0) {
                return null;
            }
            startind = content.IndexOf('"', startind + "\"DiagnosticsUrl\"".Length);
            if (startind < 0) {
                return null;
            }
            var endind = content.IndexOf('"', startind + 1);
            if (endind < 0) {
                return null;
            }
            return content.Substring(startind + 1, endind - startind - 1).TrimEnd('/', '\\');
        }

        private static bool IsFileUnaltered(string fileName, string fileHash)
        {
            return fileHash == null || string.Equals(FileUtils.CalculateFileHash(fileName), 
                fileHash, StringComparison.OrdinalIgnoreCase);
        }

        public void OnLoad()
        {
            // load servant.dll
            var fiddlerUserPath = CONFIG.GetPath("AutoFiddlers_User");
            var asm = Assembly.UnsafeLoadFrom(Path.Combine(fiddlerUserPath, BishopDllName));
            if (asm == null) {
                throw new InvalidOperationException("Bishop not found.");
            }
            var t = asm.GetType("LowLevelDesign.Diagnostics.Bishop.FiddlerPlugin");
            if (t == null) {
                throw new InvalidOperationException("Bishop.dll is not valid - does not contain the plugin class.");
            }
            var s = (IAutoTamper)Activator.CreateInstance(t);
            s.OnLoad();

            bishop = s;
        }
        public void OnBeforeUnload()
        {
            if (bishop != null)
            {
                bishop.OnBeforeUnload();
            }
        }

        public void AutoTamperRequestAfter(Session oSession)
        {
            if (bishop != null)
            {
                bishop.AutoTamperRequestAfter(oSession);
            }
        }

        public void AutoTamperRequestBefore(Session oSession)
        {
            if (bishop != null)
            {
                bishop.AutoTamperRequestBefore(oSession);
            }
        }

        public void AutoTamperResponseAfter(Session oSession)
        {
            if (bishop != null)
            {
                bishop.AutoTamperResponseAfter(oSession);
            }
        }

        public void AutoTamperResponseBefore(Session oSession)
        {
            if (bishop != null)
            {
                bishop.AutoTamperResponseAfter(oSession);
            }
        }

        public void OnBeforeReturningError(Session oSession)
        {
            if (bishop != null)
            {
                bishop.OnBeforeReturningError(oSession);
            }
        }
    }
}
