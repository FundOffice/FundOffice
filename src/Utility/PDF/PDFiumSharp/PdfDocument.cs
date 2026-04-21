#region Copyright and License
/*
This file is part of PDFiumSharp, a wrapper around the PDFium library for the .NET framework.
Copyright (C) 2017 Tobias Meyer
License: Microsoft Reciprocal License (MS-RL)
*/
#endregion
using System;
using System.Collections.Generic;
using PDFiumSharp.Types;
using System.IO;
using System.Runtime.InteropServices;
using PDFiumSharp.Enums;

namespace PDFiumSharp
{
    public sealed class PdfDocument : NativeWrapper<FPDF_DOCUMENT>
    {
        private FPDF_FORMFILLINFO formCallbacks;

        private GCHandle formCallbacksHandle;

        private FPDF_FORMHANDLE form;

        internal FPDF_FORMHANDLE Form => this.form;

        /// <summary>
        /// Gets the pages in the current <see cref="PdfDocument"/>.
        /// </summary>
        public PdfPageCollection Pages { get; }

        public PdfDestinationCollection Destinations { get; }

        /// <summary>
        /// Gets the PDF file version. File version: 14 for 1.4, 15 for 1.5, ...
        /// </summary>
        public int FileVersion
        {
            get
            {
                PDFium.FPDF_GetFileVersion(this.Handle, out var fileVersion);
                return fileVersion;
            }
        }

        /// <summary>
        /// Gets the revision of the security handler.
        /// </summary>
        /// <seealso href="http://wwwimages.adobe.com/content/dam/Adobe/en/devnet/pdf/pdfs/PDF32000_2008.pdf">PDF Reference: Table 21</seealso>
        public int SecurityHandlerRevision => PDFium.FPDF_GetSecurityHandlerRevision(this.Handle);

        public DocumentPermissions Permissions => PDFium.FPDF_GetDocPermissions(this.Handle);

        public bool PrintPrefersScaling => PDFium.FPDF_VIEWERREF_GetPrintScaling(this.Handle);

        public int PrintCopyCount => PDFium.FPDF_VIEWERREF_GetNumCopies(this.Handle);

        public DuplexTypes DuplexType => PDFium.FPDF_VIEWERREF_GetDuplex(this.Handle);

        public IEnumerable<PdfBookmark> Bookmarks
        {
            get
            {
                var handle = PDFium.FPDFBookmark_GetFirstChild(this.Handle, FPDF_BOOKMARK.Null);
                while (!handle.IsNull)
                {
                    yield return new PdfBookmark(this, handle);
                    handle = PDFium.FPDFBookmark_GetNextSibling(this.Handle, handle);
                }
            }
        }

        public PageModes PageMode => PDFium.FPDFDoc_GetPageMode(this.Handle);

        private PdfDocument(FPDF_DOCUMENT doc)
            : base(doc)
        {
            if (doc.IsNull)
            {
                throw new PDFiumException();
            }

            // The version depends on whether PDFium was compiled with XFA (2) or without (1).
            // Try in sequential order.
            for (var version = 1; version <= 2; version++)
            {
                this.formCallbacks = new FPDF_FORMFILLINFO(version);

                // We have to pin the callbacks because the form references them (so they shouldn't ever be moved)
                this.formCallbacksHandle = GCHandle.Alloc(this.formCallbacks, GCHandleType.Pinned);
                this.form = PDFium.FPDFDOC_InitFormFillEnvironment(doc, this.formCallbacks);
                if (!this.form.IsNull)
                {
                    // copied from https://pdfium.googlesource.com/pdfium/+/refs/heads/main/samples/pdfium_test.cc#1272
                    PDFium.FPDF_SetFormFieldHighlightColor(this.form, 0, 0xFFE4DD);
                    PDFium.FPDF_SetFormFieldHighlightAlpha(this.form, 100);
                    PDFium.FORM_DoDocumentJSAction(this.form);
                    PDFium.FORM_DoDocumentOpenAction(this.form);
                    break;
                }

                // that's not a correct version - unpin the handle
                this.formCallbacksHandle.Free();
            }

            this.Pages = new PdfPageCollection(this);
            this.Destinations = new PdfDestinationCollection(this);
        }

        /// <summary>
        /// Creates a new <see cref="PdfDocument"/>.
        /// <see cref="Close"/> must be called in order to free unmanaged resources.
        /// </summary>
        public PdfDocument()
            : this(PDFium.FPDF_CreateNewDocument()) { }

        /// <summary>
        /// Loads a <see cref="PdfDocument"/> from the file system.
        /// <see cref="Close"/> must be called in order to free unmanaged resources.
        /// </summary>
        /// <param name="fileName">Filepath of the PDF file to load.</param>
        /// <param name="password">Password.</param>
        public PdfDocument(string fileName, string password = null)
            : this(PDFium.FPDF_LoadDocument(fileName, password)) { }

        /// <summary>
        /// Loads a <see cref="PdfDocument"/> from memory.
        /// <see cref="Close"/> must be called in order to free unmanaged resources.
        /// </summary>
        /// <param name="data">Byte array containing the bytes of the PDF document to load.</param>
        /// <param name="index">The index of the first byte to be copied from <paramref name="data"/>.</param>
        /// <param name="count">The number of bytes to copy from <paramref name="data"/> or a negative value to copy all bytes.</param>
        /// <param name="password">Password.</param>
        public PdfDocument(byte[] data, int index = 0, int count = -1, string password = null)
            : this(PDFium.FPDF_LoadDocument(data, index, count, password)) { }

        /// <summary>
        /// Loads a <see cref="PdfDocument"/> from '<paramref name="count"/>' bytes read from a <paramref name="stream"/>.
        /// <see cref="Close"/> must be called in order to free unmanaged resources.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="fileRead"></param>
        /// <param name="count">
        /// The number of bytes to read from the <paramref name="stream"/>.
        /// If the value is equal to or smaller than 0, the stream is read to the end.
        /// </param>
        /// <param name="password"></param>
        public PdfDocument(Stream stream, FPDF_FILEREAD fileRead, int count = 0, string password = null)
            : this(PDFium.FPDF_LoadDocument(stream, fileRead, count, password)) { }

        /// <summary>
        /// Closes the <see cref="PdfDocument"/> and frees unmanaged resources.
        /// </summary>
        public void Close() => ((IDisposable)this).Dispose();

        /// <summary>
        /// Saves the <see cref="PdfDocument"/> to a <paramref name="stream"/>.
        /// </summary>
        /// <param name="flags">Flags</param>
        /// <param name="version">
        /// The new PDF file version of the saved file.
        /// 14 for 1.4, 15 for 1.5, etc. Values smaller than 10 are ignored.
        /// </param>
        /// <param name="stream">Pdf stream.</param>
        public bool Save(Stream stream, SaveFlags flags = SaveFlags.None, int version = 0)
            => PDFium.FPDF_SaveAsCopy(this.Handle, stream, flags, version);

        /// <summary>
        /// Saves the <see cref="PdfDocument"/> to the file system.
        /// </summary>
        /// <param name="flags">Save flags.</param>
        /// <param name="version">
        /// The new PDF file version of the saved file.
        /// 14 for 1.4, 15 for 1.5, etc. Values smaller than 10 are ignored.
        /// </param>
        /// <param name="filename">File name.</param>
        public bool Save(string filename, SaveFlags flags = SaveFlags.None, int version = 0)
        {
            using (var stream = new FileStream(filename, FileMode.Create))
            {
                return this.Save(stream, flags, version);
            }
        }

        public PdfBookmark FindBookmark(string title)
        {
            var handle = PDFium.FPDFBookmark_Find(this.Handle, title);
            return handle.IsNull ? null : new PdfBookmark(this, handle);
        }

        public string GetMetaText(MetadataTags tag) => PDFium.FPDF_GetMetaText(this.Handle, tag);

        public void CopyViewerPreferencesFrom(PdfDocument srcDoc) => PDFium.FPDF_CopyViewerPreferences(this.Handle, srcDoc.Handle);

        protected override void Dispose(FPDF_DOCUMENT handle)
        {
            if (!this.form.IsNull)
            {
                PDFium.FORM_DoDocumentAAction(this.form, FPDFDOC_AACTION.WC);
                PDFium.FPDFDOC_ExitFormFillEnvironment(this.form);
                this.form = FPDF_FORMHANDLE.Null;
                this.formCallbacks = null;

                if (this.formCallbacksHandle.IsAllocated)
                {
                    this.formCallbacksHandle.Free();
                }
            }

            ((IDisposable)this.Pages).Dispose();
            PDFium.FPDF_CloseDocument(handle);
        }
    }
}