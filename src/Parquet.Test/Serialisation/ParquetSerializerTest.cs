﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Parquet.Data;
using Parquet.File.Values.Primitives;
using Parquet.Schema;
using Parquet.Serialization;
using Parquet.Serialization.Attributes;
using Parquet.Test.Xunit;
using Xunit;

namespace Parquet.Test.Serialisation {

    /// <summary>
    /// When writing tests for this class, please note that you must write the test
    /// for strong typed entities, and for untyped dictionaries. This is because
    /// assembler and shredder has slightly different code paths for untyped dictionaries,
    /// despite majority of the code being shared.
    /// Make the tests small and focused, and use the same test data for both. It helps to debug
    /// expression trees easier.
    /// </summary>
    public class ParquetSerializerTest : TestBase {

        private async Task Compare<T>(List<T> data, bool asJson = false) where T : new() {

            // serialize to parquet
            using var ms = new MemoryStream();
            await ParquetSerializer.SerializeAsync(data, ms);

            // deserialize from parquet
            ms.Position = 0;
            IList<T> data2 = await ParquetSerializer.DeserializeAsync<T>(ms);

            // compare
            if(asJson) {
                XAssert.JsonEquivalent(data, data2);
            } else {
                Assert.Equivalent(data2, data);
            }
        }

        private async Task DictCompare<TSchema>(List<Dictionary<string, object>> data, bool asJson = false,
            string? writeTestFile = null) {

            // serialize to parquet
            using var ms = new MemoryStream();
            await ParquetSerializer.SerializeAsync(typeof(TSchema).GetParquetSchema(true), data, ms);

            if(writeTestFile != null) {
                System.IO.File.WriteAllBytes(writeTestFile, ms.ToArray());
            }

            // deserialize from parquet
            ms.Position = 0;
            ParquetSerializer.UntypedResult data2 = await ParquetSerializer.DeserializeAsync(ms);

            // compare
            if(asJson) {
                XAssert.JsonEquivalent(data, data2.Data);
            }
            else {
                Assert.Equivalent(data2.Data, data);
            }
        }

        class Record {
            public DateTime Timestamp { get; set; }
            public string? EventName { get; set; }
            public double MeterValue { get; set; }

            public Guid ExternalId { get; set; }
        }

        [Fact]
        public async Task Atomics_Simplest_Serde() {

            var data = Enumerable.Range(0, 1_000).Select(i => new Record {
                Timestamp = DateTime.UtcNow.AddSeconds(i),
                EventName = i % 2 == 0 ? "on" : "off",
                MeterValue = i,
                ExternalId = Guid.NewGuid()
            }).ToList();

            await Compare(data);
        }

        [Fact]
        public async Task Atomics_Simplest_Serde_Dict() {

            var data = Enumerable.Range(0, 1_000).Select(i => new Dictionary<string, object> {
                ["Timestamp"] = DateTime.UtcNow.AddSeconds(i),
                ["EventName"] = i % 2 == 0 ? "on" : "off",
                ["MeterValue"] = (double)i,
                ["ExternalId"] = Guid.NewGuid()
            }).ToList();

            await DictCompare<Record>(data);
        }

        class RecordWithNewField : Record {
            public DateTime NewTimestamp { get; set; }
            public string? NewEventName { get; set; }
            public double NewMeterValue { get; set; } = 123;

            public Guid NewExternalId { get; set; }
        }

        /// <summary>
        /// Class specific, no case for untyped dictionaries
        /// </summary>
        [Fact]
        public async Task Atomics_With_New_Field_Skipped() {

            var data = Enumerable.Range(0, 1_000).Select(i => new Record {
                Timestamp = DateTime.UtcNow.AddSeconds(i),
                EventName = i % 2 == 0 ? "on" : "off",
                MeterValue = i,
                ExternalId = Guid.NewGuid()
            }).ToList();

            using var ms = new MemoryStream();
            ParquetSchema schema = await ParquetSerializer.SerializeAsync(data, ms);

            ms.Position = 0;
            IList<RecordWithNewField> data2 = await ParquetSerializer.DeserializeAsync<RecordWithNewField>(
                ms);

            Assert.Equal(data.Count, data2.Count);
            Assert.Equal(default, data2.First().NewTimestamp);
            Assert.Null(data2.First().NewEventName);
            Assert.Equal(123, data2.First().NewMeterValue);
            Assert.Equal(default, data2.First().NewExternalId);
        }

        class NullableRecord : Record {
            public int? ParentId { get; set; }
        }

        [Fact]
        public async Task Atomics_Nullable_Serde() {

            var data = Enumerable.Range(0, 1_000).Select(i => new NullableRecord {
                Timestamp = DateTime.UtcNow.AddSeconds(i),
                EventName = i % 2 == 0 ? "on" : "off",
                MeterValue = i,
                ParentId = (i % 4 == 0) ? null : i
            }).ToList();

            await Compare(data);
        }

        [Fact]
        public async Task Atomics_Nullable_Serde_Dict() {

            var data = Enumerable.Range(0, 1_000).Select(i => new Dictionary<string, object> {
                ["Timestamp"] = DateTime.UtcNow.AddSeconds(i),
                ["EventName"] = i % 2 == 0 ? "on" : "off",
                ["MeterValue"] = (double)i,
                ["ParentId"] = (i % 4 == 0) ? null : i,
                ["ExternalId"] = Guid.NewGuid()
            }).ToList();

            await DictCompare<NullableRecord>(data);
        }

        class Primitives {
            [ParquetSimpleRepeatable]
            public List<bool>? Booleans { get; set; }

            [ParquetSimpleRepeatable]
            public List<short>? Shorts { get; set; }

            [ParquetSimpleRepeatable]
            public List<ushort>? UShorts { get; set; }

            [ParquetSimpleRepeatable]
            public List<int>? Integers { get; set; }

            [ParquetSimpleRepeatable]
            public List<uint>? UIntegers { get; set; }

            [ParquetSimpleRepeatable]
            public List<long>? Longs { get; set; }

            [ParquetSimpleRepeatable]
            public List<ulong>? ULongs { get; set; }

            [ParquetSimpleRepeatable]
            public List<float>? Floats { get; set; }

            [ParquetSimpleRepeatable]
            public List<double>? Doubles { get; set; }

            [ParquetSimpleRepeatable]
            public List<decimal>? Decimals { get; set; }

            [ParquetSimpleRepeatable]
            public List<DateTime>? DateTimes { get; set; }

            [ParquetSimpleRepeatable]
            public List<TimeSpan>? TimeSpans { get; set; }

            [ParquetSimpleRepeatable]
            public List<Interval>? Intervals { get; set; }

            [ParquetSimpleRepeatable]
            public List<string>? Strings { get; set; }
        }

        [Theory]
        [InlineData("legacy_primitives_collection_arrays.parquet")]
        public async Task ParquetSerializer_ReadingLegacyPrimitivesCollectionArrayAsync(string parquetFile) {
            IList<Primitives> data = await ParquetSerializer.DeserializeAsync<Primitives>(OpenTestFile(parquetFile));

            Assert.NotNull(data);
            Assert.Single(data);

            Primitives element = data[0];
            Assert.Equivalent(element.Booleans, new[] { true, false });
            Assert.Equivalent(element.Shorts, new short[] { 1, 2 });
            Assert.Equivalent(element.UShorts, new ushort[] { 1, 2 });
            Assert.Equivalent(element.Integers, new int[] { 1, 2 });
            Assert.Equivalent(element.UIntegers, new uint[] { 1, 2 });
            Assert.Equivalent(element.Longs, new long[] { 1, 2 });
            Assert.Equivalent(element.ULongs, new ulong[] { 1, 2 });
            Assert.Equivalent(element.Floats, new float[] { 1.2f, 1.3f });
            Assert.Equivalent(element.Doubles, new double[] { 1.2, 1.3 });
            Assert.Equivalent(element.Decimals, new decimal[] { 1.2m, 1.3m });
            Assert.Equivalent(element.DateTimes, new DateTime[] { new DateTime(2023, 01, 01), new DateTime(2023, 02, 01, 16, 30, 05) });
            Assert.Equivalent(element.TimeSpans, new TimeSpan[] { TimeSpan.Zero, TimeSpan.FromSeconds(5) });
            Assert.Equivalent(element.Intervals, new[] { new Interval(0, 0, 0), new Interval(0, 1, 1) });
            Assert.Equivalent(element.Strings, new[] { "Hello", "People" });
        }

        class Address {
            public string? Country { get; set; }

            public string? City { get; set; }
        }

        class AddressBookEntry {
            
            public string? FirstName { get; set; }

            public string? LastName { get; set; }

            public Address? Address { get; set; }
        }

        [Fact]
        public async Task Struct_Serde() {

            var data = Enumerable.Range(0, 1_000).Select(i => new AddressBookEntry {
                FirstName = "Joe",
                LastName = "Bloggs",
                Address = new Address() {
                    Country = "UK",
                    City = "Unknown"
                }
            }).ToList();

            await Compare(data);
        }

        [Fact]
        public async Task Struct_Serde_Dict() {

            var data = Enumerable.Range(0, 1_000).Select(i => new Dictionary<string, object> {
                ["FirstName"] = "Joe",
                ["LastName"] = "Bloggs",
                ["Address"] = new Dictionary<string, object> {
                    ["Country"] = "UK",
                    ["City"] = "Unknown"
                }
            }).ToList();

            await DictCompare<AddressBookEntry>(data);
        }


        class MovementHistory {
            public int? PersonId { get; set; }

            public string? Comments { get; set; }

            public List<Address>? Addresses { get; set; }
        }

        [Fact]
        public async Task Struct_WithNullProps_SerdeAsync() {

            var data = Enumerable.Range(0, 10).Select(i => new AddressBookEntry {
                FirstName = "Joe",
                LastName = "Bloggs"
                // Address is null
            }).ToList();

            using var ms = new MemoryStream();
            ParquetSchema schema = await ParquetSerializer.SerializeAsync(data, ms);

            // Address.Country/City must be RL: 0, DL: 0 as Address is null

            ms.Position = 0;
            IList<AddressBookEntry> data2 = await ParquetSerializer.DeserializeAsync<AddressBookEntry>(ms);

            XAssert.JsonEquivalent(data, data2);
        }

        [Fact]
        public async Task Struct_With_NestedNulls_SerdeAsync() {

            var data = new List<AddressBookEntry> {
                new AddressBookEntry {
                    FirstName = "Joe",
                    LastName = "Bloggs",
                    Address = new Address() {
                        City = null,
                        Country = null
                    }
                }
            };
            
            await Compare(data);
        }

        [Fact]
        public async Task List_Structs_Serde() {
            var data = Enumerable.Range(0, 1_000).Select(i => new MovementHistory {
                PersonId = i,
                Comments = i % 2 == 0 ? "none" : null,
                Addresses = Enumerable.Range(0, 4).Select(a => new Address {
                    City = "Birmingham",
                    Country = "United Kingdom"
                }).ToList()
            }).ToList();

            await Compare(data);
        }

        [Fact]
        public async Task List_Structs_Serde_Dict() {
            var data = Enumerable.Range(0, 1_000).Select(i => new Dictionary<string, object> {
                ["PersonId"] = i,
                ["Comments"] = i % 2 == 0 ? "none" : null,
                ["Addresses"] = Enumerable.Range(0, 4).Select(a => new Dictionary<string, object> {
                    ["City"] = "Birmingham",
                    ["Country"] = "United Kingdom"
                }).ToList()
            }).ToList();

            await DictCompare<MovementHistory>(data);
        }


        [Fact]
        public async Task List_Null_Structs_Serde() {
            var data = Enumerable.Range(0, 1_000).Select(i => new MovementHistory {
                PersonId = i,
                Comments = i % 2 == 0 ? "none" : null,
                Addresses = null
            }).ToList();

            await Compare(data);
        }

        [Fact]
        public async Task List_Empty_Structs_Serde() {
            var data = Enumerable.Range(0, 1_000).Select(i => new MovementHistory {
                PersonId = i,
                Comments = i % 2 == 0 ? "none" : null,
                Addresses = new()
            }).ToList();

            await Compare(data);
        }

        class MovementHistoryCompressed {
            public int? PersonId { get; set; }

            public List<int>? ParentIds { get; set; }
        }

        [Fact]
        public async Task List_Atomics_Serde() {

            var data = Enumerable.Range(0, 100).Select(i => new MovementHistoryCompressed {
                PersonId = i,
                ParentIds = Enumerable.Range(i, 4).ToList()
            }).ToList();

            await Compare(data);
        }

        [Fact]
        public async Task List_Atomics_Serde_Dict() {

            var data = Enumerable.Range(0, 100).Select(i => new Dictionary<string, object> {
                ["PersonId"] = i,
                ["ParentIds"] = Enumerable.Range(i, 4).ToList()
            }).ToList();

            await DictCompare<MovementHistoryCompressed>(data);
        }

        [Fact]
        public async Task List_Atomics_Empty_Serde() {

            var data = new List<MovementHistoryCompressed> {
                new MovementHistoryCompressed {
                    PersonId = 0,
                    ParentIds = new() { 1, 2 }
                },
                new MovementHistoryCompressed {
                    PersonId = 1,
                    ParentIds = new List<int>()
                },
                new MovementHistoryCompressed {
                    PersonId = 2,
                    ParentIds = new() { 3, 4, 5 }
                }
            };

            // serialise
            using var ms = new MemoryStream();
            await ParquetSerializer.SerializeAsync(data, ms);


            // low-level validate that the file has correct levels
            ms.Position = 0;
            List<Data.DataColumn> cols = await ReadColumns(ms);
            DataColumn pidsCol = cols[1];
            Assert.Equal(new int[] { 1, 2, 3, 4, 5 }, pidsCol.DefinedData);
            Assert.Equal(new int[] { 2, 2, 1, 2, 2, 2 }, pidsCol.DefinitionLevels);
            Assert.Equal(new int[] { 0, 1, 0, 0, 1, 1 }, pidsCol.RepetitionLevels);


            // deserialise
            ms.Position = 0;
            IList<MovementHistoryCompressed> data2 = await ParquetSerializer.DeserializeAsync<MovementHistoryCompressed>(ms);

            // assert
            XAssert.JsonEquivalent(data, data2);
        }

        [Fact]
        public async Task List_Atomics_Null_Serde() {

            var data = new List<MovementHistoryCompressed> {
                new MovementHistoryCompressed {
                    PersonId = 0,
                    ParentIds = new() { 1, 2 }
                },
                new MovementHistoryCompressed {
                    PersonId = 1,
                    ParentIds = null
                },
                new MovementHistoryCompressed {
                    PersonId = 2,
                    ParentIds = new() { 3, 4 }
                }
            };

            // serialise
            using var ms = new MemoryStream();
            await ParquetSerializer.SerializeAsync(data, ms);

            // low-level validate that the file has correct levels
            ms.Position = 0;
            List<Data.DataColumn> cols = await ReadColumns(ms);
            DataColumn pidsCol = cols[1];
            Assert.Equal(new int[] { 1, 2, 3, 4 }, pidsCol.DefinedData);
            Assert.Equal(new int[] { 2, 2, 0, 2, 2 }, pidsCol.DefinitionLevels);
            Assert.Equal(new int[] { 0, 1, 0, 0, 1 }, pidsCol.RepetitionLevels);

            // deserialise
            ms.Position = 0;
            IList<MovementHistoryCompressed> data2 = await ParquetSerializer.DeserializeAsync<MovementHistoryCompressed>(ms);

            // assert
            XAssert.JsonEquivalent(data, data2);
        }


        class IdWithTags {
            public int Id { get; set; }

            public Dictionary<string, string>? Tags { get; set; }
        }


        [Fact]
        public async Task Map_Simple_Serde() {
            var data = Enumerable.Range(0, 10).Select(i => new IdWithTags {
                Id = i,
                Tags = new Dictionary<string, string> {
                    ["id"] = i.ToString(),
                    ["gen"] = DateTime.UtcNow.ToString()
                }}).ToList();

            await Compare(data, true);
        }

        [Fact]
        public async Task Map_Simple_Serde_Dict() {
            var data = Enumerable.Range(0, 10).Select(i => new Dictionary<string, object> {
                ["Id"] = i,
                ["Tags"] = new Dictionary<string, string> {
                    ["id"] = i.ToString(),
                    ["gen"] = DateTime.UtcNow.ToString()
                }
            }).ToList();

            await DictCompare<IdWithTags>(data, true);
        }

        [Fact]
        public async Task Append_reads_all_data() {
            var data = Enumerable.Range(0, 1_000).Select(i => new Record {
                Timestamp = DateTime.UtcNow.AddSeconds(i),
                EventName = i % 2 == 0 ? "on" : "off",
                MeterValue = i
            }).ToList();

            using var ms = new MemoryStream();

            const int batchSize = 100;
            for(int i = 0; i < data.Count; i += batchSize) {
                List<Record> dataBatch = data.Skip(i).Take(batchSize).ToList();

                ms.Position = 0;
                await ParquetSerializer.SerializeAsync(dataBatch, ms, new ParquetSerializerOptions { Append = i > 0 });
            }

            ms.Position = 0;
            IList<Record> data2 = await ParquetSerializer.DeserializeAsync<Record>(ms);

            Assert.Equivalent(data2, data);
        }

        [Fact]
        public async Task Append_to_new_file_fails() {
            var data = Enumerable.Range(0, 10).Select(i => new Record {
                Timestamp = DateTime.UtcNow.AddSeconds(i),
                EventName = i % 2 == 0 ? "on" : "off",
                MeterValue = i
            }).ToList();

            using var ms = new MemoryStream();
            await Assert.ThrowsAsync<IOException>(async () => await ParquetSerializer.SerializeAsync(data, ms, new ParquetSerializerOptions { Append = true }));
        }

        [Fact]
        public async Task Specify_row_group_size() {
            var data = Enumerable.Range(0, 100).Select(i => new Record {
                Timestamp = DateTime.UtcNow.AddSeconds(i),
                EventName = i % 2 == 0 ? "on" : "off",
                MeterValue = i
            }).ToList();

            using var ms = new MemoryStream();
            await ParquetSerializer.SerializeAsync(data, ms, new ParquetSerializerOptions { RowGroupSize = 20 });

            // validate we have 5 row groups in the resulting file
            ms.Position = 0;
            using ParquetReader reader = await ParquetReader.CreateAsync(ms);
            Assert.Equal(5, reader.RowGroupCount);
        }

        [Fact]
        public async Task Deserialize_per_row_group() {
            DateTime now = DateTime.UtcNow;
            int records = 100;
            int rowGroupSize = 20;

            var data = Enumerable.Range(0, records).Select(i => new Record {
                Timestamp = now.AddSeconds(i),
                EventName = i % 2 == 0 ? "on" : "off",
                MeterValue = i
            }).ToList();

            using var ms = new MemoryStream();
            await ParquetSerializer.SerializeAsync(data, ms, new ParquetSerializerOptions { RowGroupSize = rowGroupSize });

            ms.Position = 0;
            for(int i = 0; i < records; i += rowGroupSize) {
                IList<Record> data2 = await ParquetSerializer.DeserializeAsync<Record>(ms, i / rowGroupSize);
                Assert.Equivalent(data.Skip(i).Take(rowGroupSize), data2);
            }
        }

        [Fact]
        public async Task Deserialize_as_async_enumerable() {
            DateTime now = DateTime.UtcNow;
            int records = 100;
            int rowGroupSize = 20;

            var data = Enumerable.Range(0, records).Select(i => new Record {
                Timestamp = now.AddSeconds(i),
                EventName = i % 2 == 0 ? "on" : "off",
                MeterValue = i
            }).ToList();

            using var ms = new MemoryStream();
            await ParquetSerializer.SerializeAsync(data, ms, new ParquetSerializerOptions { RowGroupSize = rowGroupSize });

            ms.Position = 0;
            IList<Record> data2 = await ParquetSerializer.DeserializeAllAsync<Record>(ms).ToArrayAsync();

            Assert.Equivalent(data, data2);
        }

        [Fact]
        public async Task Specify_row_group_size_too_small() {
            var data = Enumerable.Range(0, 100).Select(i => new Record {
                Timestamp = DateTime.UtcNow.AddSeconds(i),
                EventName = i % 2 == 0 ? "on" : "off",
                MeterValue = i
            }).ToList();

            using var ms = new MemoryStream();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => ParquetSerializer.SerializeAsync(data, ms, new ParquetSerializerOptions { RowGroupSize = 0 }));
        }

        class StringRequiredAndNot {
            [ParquetRequired]
            public string String { get; set; } = string.Empty;
            public string? Nullable { get; set; }
        }

        [Fact]
        public async Task Deserialize_required_strings() {
            var expected = new StringRequiredAndNot[] {
                new() { String = "a", Nullable = null },
                new() { String = "b", Nullable = "y" },
                new() { String = "c", Nullable = "z" },
            };

            await using Stream stream = OpenTestFile("required-strings.parquet");
            IList<StringRequiredAndNot> actual = await ParquetSerializer.DeserializeAsync<StringRequiredAndNot>(stream);

            Assert.Equivalent(expected, actual);
        }

        class AddressBookEntryAlias {

            public string? Name { get; set; }

            [JsonPropertyName("Address")]
            public Address? _address { get; set; }

            [JsonPropertyName("PhoneNumbers")]
            public List<string>? _phoneNumbers { get; set; }
        }

        [Fact]
        public async Task List_Struct_WithAlias_Serde() {

            var data = Enumerable.Range(0, 1_000).Select(i => new AddressBookEntryAlias {
                Name = "Joe",
                _address = new Address() {
                    Country = "UK",
                    City = "Unknown",
                },
                _phoneNumbers = new List<string>() {
                    "123-456-7890",
                    "111-222-3333"
                }
            }).ToList();

            await Compare(data);
        }
    }
}
