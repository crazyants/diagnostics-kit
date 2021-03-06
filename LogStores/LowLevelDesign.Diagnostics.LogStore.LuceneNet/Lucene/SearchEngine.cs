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

using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace LowLevelDesign.Diagnostics.LogStore.LuceneNet.Lucene
{
    /// <summary>
    /// Very generic engine based on Lucene.
    /// </summary>
    internal sealed class SearchEngine : IDisposable 
    {
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        private class IndexSearcherGuard : IDisposable
        {
            private IndexSearcher indexSearcher;
            private readonly ReaderWriterLockSlim searcherLock = new ReaderWriterLockSlim();

            public IndexSearcherGuard(IndexReader reader) {
                this.indexSearcher = new IndexSearcher(reader);
            }

            public IndexSearcher Get() {
                searcherLock.EnterReadLock();
                try {
                    return indexSearcher;
                } finally {
                    searcherLock.ExitReadLock();
                }
            }

            public void Reopen() {
                var newIndexReader = indexSearcher.IndexReader.Reopen();
                if (newIndexReader != indexSearcher.IndexReader) {
                    IndexSearcher prevIndexSearcher;
                    // means that index was recreated - we need to reload index searcher
                    searcherLock.EnterWriteLock();
                    try {
                        prevIndexSearcher = indexSearcher;
                        indexSearcher = new IndexSearcher(newIndexReader);
                    } finally {
                        searcherLock.ExitWriteLock();
                    }
                    // now get rid of previous index searcher
                    Debug.Assert(prevIndexSearcher != null);
                    prevIndexSearcher.IndexReader.Dispose();
                    prevIndexSearcher.Dispose();
                }
            }

            ~IndexSearcherGuard() {
                Dispose(true);
            }

            public void Dispose() {
                Dispose(false);
            }

            private void Dispose(bool disposing) {
                var ind = this.indexSearcher;
                this.indexSearcher = null;
                ind.IndexReader.Dispose();
                ind.Dispose();
                if (!disposing) {
                    this.searcherLock.Dispose();
                }
            }
        }

        private readonly String indexPath;

        private readonly IndexSearcherGuard indexSearcherGuard;
        private readonly StreamWriter logWriter;
        private readonly Func<Analyzer> analyzerBuilder;

        public SearchEngine(String indexPath, Func<Analyzer> analyzerBuilder, String logPath = null) {
            if (logPath != null) {
                logWriter = new StreamWriter(logPath);
                logWriter.AutoFlush = true;
            }

            this.analyzerBuilder = analyzerBuilder;
            this.indexPath = indexPath;
            try {
                this.indexSearcherGuard = new IndexSearcherGuard(IndexReader.Open(FSDirectory.Open(indexPath), true));
            } catch (FileNotFoundException) {
                logger.Info("There was a problem when creating IndexSearcher - probably the index was removed. We will try to recreate it.");
                using (var writer = CreateIndexWriter()) {
                }
                this.indexSearcherGuard = new IndexSearcherGuard(IndexReader.Open(FSDirectory.Open(indexPath), true));
            }
        }

        public IEnumerable<Document> FindDocuments(Query query, int top) {
            Debug.Assert(top > 0);

            var hits = indexSearcherGuard.Get().Search(query, top);
            var foundDocs = new Document[hits.ScoreDocs.Length];
            for (int i = 0; i < foundDocs.Length; i++) {
                foundDocs[i] = indexSearcherGuard.Get().Doc(hits.ScoreDocs[i].Doc);
            }
            return foundDocs;
        }

        public SearchResults<Document> FindDocuments(Query query, Sort sort, int itemsToReturn, int offset) {
            Debug.Assert(itemsToReturn > 0);
            Debug.Assert(offset >= 0);
            if (itemsToReturn <= 0 || offset < 0)
                throw new ArgumentException();

            var result = new SearchResults<Document> {
                ItemsToReturn = itemsToReturn,
                Offset = offset
            };

            // populate found rivers information
            var hits = indexSearcherGuard.Get().Search(query, null, offset + itemsToReturn, sort);

            result.MaxItemsNumber = hits.TotalHits;
            // how many results should we return
            int resultsCount = hits.ScoreDocs.Length - offset;
            if (resultsCount <= 0) {
                result.FoundItems = new Document[0];
                return result;
            }

            int startind = offset;
            var foundDocs = new Document[resultsCount];
            for (int i = 0; i < foundDocs.Length; i++) {
                foundDocs[i] = indexSearcherGuard.Get().Doc(hits.ScoreDocs[startind + i].Doc);
            }
            result.FoundItems = foundDocs;

            return result;
        }

        private IndexWriter CreateIndexWriter() {
            var iw = new IndexWriter(FSDirectory.Open(new DirectoryInfo(indexPath), new SimpleFSLockFactory()),
                                            analyzerBuilder(), IndexWriter.MaxFieldLength.UNLIMITED);
            // enable logging if set
            if (logWriter != null) {
                iw.SetInfoStream(logWriter);
            }
            return iw;
        }

        public void SaveDocumentInIndex(Document doc) {
            using (var iw = CreateIndexWriter()) {
                iw.AddDocument(doc);
                iw.Commit();
            }
            // we need to refresh the index searcher in case something changed
            indexSearcherGuard.Reopen();
        }

        public void ReplaceDocumentInIndex(Query deleteQuery, Document doc) {
            using (var iw = CreateIndexWriter()) {
                iw.DeleteDocuments(deleteQuery);
                iw.AddDocument(doc);
                iw.Commit();
            }
            indexSearcherGuard.Reopen();
        }

        public void DeleteDocumentFromIndex(Term term) {
            using (var iw = CreateIndexWriter()) {
                iw.DeleteDocuments(term);
                iw.Commit();
            }
            // refresh the index searcher
            indexSearcherGuard.Reopen();
        }

        public void DeleteDocumentFromIndex(Query query) {
            using (var iw = CreateIndexWriter()) {
                iw.DeleteDocuments(query);
                iw.Commit();
            }
            indexSearcherGuard.Reopen();
        }

        public void RebuildIndex() {
            // FIXME rebuild and optimize logic here
        }

        ~SearchEngine() {
            this.Dispose(false);
        }

        void Dispose(bool disposing) {
            if (disposing) {
                // dispose all other .NET stuff
                if (indexSearcherGuard != null)
                    indexSearcherGuard.Dispose();
                if (logWriter != null)
                    logWriter.Dispose();
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
