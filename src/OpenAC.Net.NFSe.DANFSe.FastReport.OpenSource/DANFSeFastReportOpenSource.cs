﻿// ***********************************************************************
// Assembly         : OpenAC.Net.NFSe.DANFSe.FastReport.OpenSource
// Author           : Rafael Dias
// Created          : 01-31-2016
//
// Last Modified By : Rafael Dias
// Last Modified On : 07-05-2018
// ***********************************************************************
// <copyright file="DANFSeFastReportOpenSource.cs" company="OpenAC.Net">
//		        		   The MIT License (MIT)
//	     		    Copyright (c) 2014 - 2021 Projeto OpenAC .Net
//
//	 Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//	 The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//	 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
// ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
// </copyright>
// <summary></summary>
// ***********************************************************************

using System;
using System.Drawing.Printing;
using System.IO;
using System.Reflection;
using FastReport;
using FastReport.Export.Html;
using FastReport.Export.PdfSimple;
using OpenAC.Net.Core.Extensions;
using OpenAC.Net.Core.Logging;
using OpenAC.Net.DFe.Core.Common;
using OpenAC.Net.NFSe.Configuracao;
using OpenAC.Net.NFSe.Nota;

namespace OpenAC.Net.NFSe.DANFSe.FastReport.OpenSource
{
    public sealed class DANFSeFastReportOpenSource : OpenDANFSeBase
    {
        #region Fields

        private Report internalReport;
        private PrinterSettings settings;

        #endregion Fields

        #region Events

        public event EventHandler<DANFSeEventArgs> OnGetReport;

        public event EventHandler<DANFSeExportEventArgs> OnExport;

        #endregion Events

        #region Constructors

        public DANFSeFastReportOpenSource(ConfigNFSe config) : base(config)
        {
        }

        #endregion Constructors

        #region Methods

        /// <inheritdoc />
        public override void Imprimir(NotaServico[] notas)
        {
            Imprimir(notas, null);
        }

        /// <inheritdoc />
        public override void ImprimirPDF(NotaServico[] notas)
        {
            var oldFiltro = Filtro;

            try
            {
                Filtro = FiltroDFeReport.PDF;
                Imprimir(notas, null);
            }
            finally
            {
                Filtro = oldFiltro;
            }
        }

        /// <inheritdoc />
        public override void ImprimirPDF(NotaServico[] notas, Stream stream)
        {
            var oldFiltro = Filtro;

            try
            {
                Filtro = FiltroDFeReport.PDF;
                Imprimir(notas, stream);
            }
            finally
            {
                Filtro = oldFiltro;
            }
        }

        /// <inheritdoc />
        public override void ImprimirHTML(NotaServico[] notas)
        {
            var oldFiltro = Filtro;

            try
            {
                Filtro = FiltroDFeReport.HTML;
                Imprimir(notas, null);
            }
            finally
            {
                Filtro = oldFiltro;
            }
        }

        /// <inheritdoc />
        public override void ImprimirHTML(NotaServico[] notas, Stream stream)
        {
            var oldFiltro = Filtro;

            try
            {
                Filtro = FiltroDFeReport.HTML;
                Imprimir(notas, stream);
            }
            finally
            {
                Filtro = oldFiltro;
            }
        }

        private void Imprimir(NotaServico[] notas, Stream stream)
        {
            try
            {
                this.Log().Debug("Iniciando impressão.");

                using (internalReport = new Report())
                {
                    PrepararImpressao();

                    this.Log().Debug("Passando dados para impressão.");

                    internalReport.RegisterData(notas, "NotaServico");
                    internalReport.Prepare();

                    switch (Filtro)
                    {
                        case FiltroDFeReport.Nenhum:
                            if (MostrarPreview)
                                internalReport.Show();
                            else if (MostrarSetup)
                                internalReport.PrintWithDialog();
                            else
                                internalReport.Print(settings);
                            break;

                        case FiltroDFeReport.PDF:
                            this.Log().Debug("Exportando para PDF.");

                            var evtPdf = new DANFSeExportEventArgs
                            {
                                Filtro = Filtro,
                                Export = new PDFSimpleExport()
                                {
                                    ImageDpi = 600,
                                    ShowProgress = MostrarSetup,
                                    OpenAfterExport = MostrarPreview
                                }
                            };

                            OnExport.Raise(this, evtPdf);
                            if (stream.IsNull())
                                internalReport.Export(evtPdf.Export, NomeArquivo);
                            else
                                internalReport.Export(evtPdf.Export, stream);

                            this.Log().Debug("Exportação concluida.");
                            break;

                        case FiltroDFeReport.HTML:
                            this.Log().Debug("Exportando para HTML.");

                            var evtHtml = new DANFSeExportEventArgs
                            {
                                Filtro = Filtro,
                                Export = new HTMLExport()
                                {
                                    Format = HTMLExportFormat.HTML,
                                    EmbedPictures = true,
                                    Preview = MostrarPreview,
                                    ShowProgress = MostrarSetup
                                }
                            };

                            OnExport.Raise(this, evtHtml);
                            if (stream.IsNull())
                                internalReport.Export(evtHtml.Export, NomeArquivo);
                            else
                                internalReport.Export(evtHtml.Export, stream);

                            this.Log().Debug("Exportação concluida.");
                            break;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    this.Log().Debug("Impressão Concluida.");
                }
            }
            finally
            {
                internalReport = null;
                settings = null;
            }
        }

        private void PrepararImpressao()
        {
            this.Log().Debug("Preparando a impressão.");

            var e = new DANFSeEventArgs(Layout);
            OnGetReport.Raise(this, e);
            if (e.FilePath.IsEmpty() || !File.Exists(e.FilePath))
            {
                //ToDo: Adicionar os layouts de acordo com o provedor
                var assembly = Assembly.GetExecutingAssembly();

                Stream ms;
                switch (Layout)
                {
                    case LayoutImpressao.ABRASF2:
                        ms = assembly.GetManifestResourceStream("OpenAC.Net.NFSe.DANFSe.FastReport.OpenSource.Report.DANFSe.frx");
                        break;

                    case LayoutImpressao.DSF:
                        ms = assembly.GetManifestResourceStream("OpenAC.Net.NFSe.DANFSe.FastReport.OpenSource.Report.DANFSe.frx");
                        break;

                    case LayoutImpressao.Ginfes:
                        ms = assembly.GetManifestResourceStream("OpenAC.Net.NFSe.DANFSe.FastReport.OpenSource.Report.DANFSe.frx");
                        break;

                    case LayoutImpressao.ABRASF:
                        ms = assembly.GetManifestResourceStream("OpenAC.Net.NFSe.DANFSe.FastReport.OpenSource.Report.DANFSe.frx");
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }

                this.Log().Debug("Carregando layout impressão.");

                internalReport.Load(ms);
            }
            else
            {
                this.Log().Debug("Carregando layout impressão costumizado.");

                internalReport.Load(e.FilePath);
            }

            this.Log().Debug("Passando configurações para o relatório.");

            internalReport.SetParameterValue("Logo", Logo);
            internalReport.SetParameterValue("LogoPrefeitura", LogoPrefeitura);
            internalReport.SetParameterValue("MunicipioPrestador", Configuracoes.WebServices.Municipio);
            internalReport.SetParameterValue("Ambiente", (int)Configuracoes.WebServices.Ambiente);
            internalReport.SetParameterValue("SoftwareHouse", SoftwareHouse);
            internalReport.SetParameterValue("Site", Site);

            settings = new PrinterSettings { Copies = (short)Math.Max(NumeroCopias, 1) };

            if (!Impressora.IsEmpty())
                settings.PrinterName = Impressora;

            this.Log().Debug("Impressão preparada.");
        }

        #endregion Methods
    }
}