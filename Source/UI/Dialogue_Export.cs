using RimWorld;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using UnityEngine;
using Verse;

namespace XmlDocumentViewer
{
    internal class Dialogue_Export : Window
    {
        string formattedXmlContent;
        string filename;
        string filepath;
        XmlDocument xmlDocument;
        bool exportCurrentContent = true;
        public override Vector2 InitialSize => new(248f + 36f, 74f + 12f + 44f + 36f);

        public Dialogue_Export(string formattedXmlContent, ref TabData tabData, string filepath = "./XmlDocumentViewer_Exports/")
        {
            closeOnAccept = false;
            forceCatchAcceptAndCancelEventEvenIfUnfocused = true;
            this.formattedXmlContent = formattedXmlContent;
            filename = tabData.documentType.ToString() + ".xml";
            xmlDocument = tabData.xmlDocument;
            this.filepath = filepath;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }
        public override void DoWindowContents(Rect inRect)
        {
            Rect labelRect = inRect.TopPartPixels(22f);
            Rect entryRect = inRect.TopPartPixels(44f).BottomPartPixels(22f);
            Rect buttonsRowRect = inRect.TopPartPixels(74f).BottomPartPixels(28f);

            Widgets.Label(labelRect, "Filename:");
            filename = Widgets.TextField(entryRect, filename);

            if (Widgets.ButtonText(buttonsRowRect.LeftHalf(), "Cancel"))
            {
                Close();
            }
            if (Widgets.ButtonText(buttonsRowRect.RightHalf(), "Export"))
            {
                TryExport();
            }

            Rect lowerRect = inRect.BottomPartPixels(12f + 44f);
            Listing_Standard listing = new();
            listing.verticalSpacing = 0;
            listing.Begin(lowerRect);
            listing.GapLine(12f);
            if (listing.RadioButton("Export current contents", exportCurrentContent))
            {
                exportCurrentContent = true;
            }
            if (listing.RadioButton("Export entire document", !exportCurrentContent))
            {
                exportCurrentContent = false;
            }
            listing.End();
        }

        // Helpers

        private void TryExport()
        {
            try
            {
                if (!filename.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    filename += ".xml";
                string exportDir = Path.GetFullPath(filepath);
                string fullPath = Path.Combine(exportDir, filename);

                if (exportCurrentContent)
                {
                    string plain = RichText.PrepareTextForCopy(formattedXmlContent);
                    string trimmed = plain.TrimStart();
                    if (!trimmed.StartsWith("<?xml", StringComparison.Ordinal))
                    {
                        plain = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + plain;
                    }

                    Directory.CreateDirectory(exportDir);
                    File.WriteAllText(fullPath, plain, new UTF8Encoding(false));
                }
                else
                {
                    XmlWriterSettings settings = new()
                    {
                        Indent = true,
                        IndentChars = "    ",
                        NewLineChars = Environment.NewLine,
                        NewLineHandling = NewLineHandling.Replace,
                        OmitXmlDeclaration = false,
                        Encoding = new UTF8Encoding(false),
                        CloseOutput = true
                    };
                    Directory.CreateDirectory(exportDir);
                    using FileStream fileStream = File.Create(fullPath);
                    using XmlWriter xmlWriter = XmlWriter.Create(fileStream, settings);
                    xmlDocument.Save(xmlWriter);
                }
                Messages.Message("Export successful", MessageTypeDefOf.TaskCompletion, false);
                OpenWithDefaultApp(fullPath);
                Close();
            }
            catch (Exception e)
            {
                Messages.Message("Export failed", MessageTypeDefOf.RejectInput, false);
            }
        }

        private static void OpenWithDefaultApp(string path)
        {
            try
            {
                ProcessStartInfo processStartInfo = new(path)
                {
                    UseShellExecute = true,
                    Verb = "open"
                };
                Process.Start(processStartInfo);
            }
            catch
            {
                // For macOS/Linux
                try
                {
#if UNITY_STANDALONE_OSX
                    Process.Start("open", "\"" + path + "\"");
#elif UNITY_STANDALONE_LINUX
                    Process.Start("xdg-open", "\"" + path + "\"");
#else
                    // As a last resort open the folder
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                        Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
#endif
                }
                catch { }
            }
        }

        public override void OnAcceptKeyPressed()
        {
            TryExport();
            Close();
        }
    }
}
