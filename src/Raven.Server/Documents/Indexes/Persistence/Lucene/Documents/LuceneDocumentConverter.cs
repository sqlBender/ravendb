﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using Lucene.Net.Documents;
using Raven.Abstractions.Data;
using Raven.Server.Utils;
using Sparrow;
using Sparrow.Binary;
using Sparrow.Json;

namespace Raven.Server.Documents.Indexes.Persistence.Lucene.Documents
{
    public class LuceneDocumentConverter : LuceneDocumentConverterBase
    {
        private readonly BlittableJsonTraverser _blittableTraverser;
        private readonly Field _reduceValueField = new Field(Constants.ReduceValueFieldName, new byte[0], 0, 0, Field.Store.YES);

        private byte[] _reduceValueBuffer;

        public LuceneDocumentConverter(ICollection<IndexField> fields, bool reduceOutput = false)
            : base(fields, reduceOutput)
        {
            if (reduceOutput)
            {
                _blittableTraverser = new BlittableJsonTraverser(new char[] { }); // map-reduce results have always flat structure
                _reduceValueBuffer = new byte[0];
            }
            else
                _blittableTraverser = BlittableJsonTraverser.Default;
        }

        protected override IEnumerable<AbstractField> GetFields(LazyStringValue key, object doc)
        {
            var document = (Document)doc;
            if (document.Key != null)
            {
                Debug.Assert(key == document.Key);

                yield return GetOrCreateKeyField(document.Key);
            }

            if (_reduceOutput)
            {
                _reduceValueField.SetValue(GetReduceResult(document.Data), 0, document.Data.Size);
                yield return _reduceValueField;
            }

            foreach (var indexField in _fields.Values)
            {
                var value = GetValue(document, indexField.Name);

                foreach (var luceneField in GetRegularFields(indexField, value))
                    yield return luceneField;
            }
        }

        private object GetValue(Document document, string path)
        {
            StringSegment leftPath;
            object value;
            if (_blittableTraverser.TryRead(document.Data, path, out value, out leftPath) == false)
            {
                value = TypeConverter.ConvertForIndexing(value);

                if (value == null)
                    return null;

                if (leftPath == "Length")
                {
                    var lazyStringValue = value as LazyStringValue;
                    if (lazyStringValue != null)
                        return lazyStringValue.Size;

                    var lazyCompressedStringValue = value as LazyCompressedStringValue;
                    if (lazyCompressedStringValue != null)
                        return lazyCompressedStringValue.UncompressedSize;

                    var array = value as BlittableJsonReaderArray;
                    if (array != null)
                        return array.Length;

                    return null;
                }

                if (leftPath == "Count")
                {
                    var array = value as BlittableJsonReaderArray;
                    if (array != null)
                        return array.Length;

                    return null;
                }

                if (value is DateTime || value is DateTimeOffset || value is TimeSpan)
                {
                    int indexOfPropertySeparator;
                    do
                    {
                        indexOfPropertySeparator = leftPath.IndexOfAny(BlittableJsonTraverser.PropertySeparators, 0);
                        if (indexOfPropertySeparator != -1)
                            leftPath = leftPath.SubSegment(0, indexOfPropertySeparator);

                        var accessor = TypeConverter.GetPropertyAccessor(value);
                        value = accessor.GetValue(leftPath, value);


                        if (value == null)
                            return null;

                    } while (indexOfPropertySeparator != -1);

                    return value;
                }

                throw new InvalidOperationException($"Could not extract {path} from {document.Key}.");
            }

            return TypeConverter.ConvertForIndexing(value);
        }

        private byte[] GetReduceResult(BlittableJsonReaderObject reduceResult)
        {
            var necessarySize = Bits.NextPowerOf2(reduceResult.Size);

            if (_reduceValueBuffer.Length < necessarySize)
                _reduceValueBuffer = new byte[necessarySize];

            unsafe
            {
                fixed (byte* v = _reduceValueBuffer)
                    reduceResult.CopyTo(v);
            }

            return _reduceValueBuffer;
        }
    }
}