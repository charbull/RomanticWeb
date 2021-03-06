﻿using System;
using System.Collections;
#if NETSTANDARD16
using System.IO;
#endif
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using RomanticWeb.DotNetRDF;
using RomanticWeb.DotNetRDF.Configuration;

namespace RomanticWeb.Tests.Configuration
{
    [TestFixture]
    public class StoresConfigurationTests : ConfigurationTest
    {
        private StoresConfigurationSection _configuration;

        [SetUp]
        public void Setup()
        {
#if NETSTANDARD16
            Directory.SetCurrentDirectory(Path.Combine(BinDirectory, "netcoreapp1.0"));
#endif
            _configuration = StoresConfigurationSection.Default;
        }

#if NETSTANDARD16
        [TearDown]
        public void Teardown()
        {
            Directory.SetCurrentDirectory(BaseDirectory);
        }
#endif

        [Test]
        public void Should_allow_to_load_thread_in_memory_store()
        {
            // given
            var store = _configuration.Stores.Single(t => t.Name == "in-memory");

            // then
            store.CreateTripleStore().Should().BeOfType<VDS.RDF.TripleStore>();
        }

        [Test]
        public void Should_allow_to_load_thread_safe_in_memory_store()
        {
            // given
            var store = _configuration.Stores.Single(t => t.Name == "threadsafe");

            // then
            store.CreateTripleStore().Should().BeOfType<VDS.RDF.ThreadSafeTripleStore>();
        }

        [Test]
        public void Should_allow_to_load_file_store()
        {
            // given
            var store = _configuration.Stores.Single(t => t.Name == "file");

            // then
            store.CreateTripleStore().Should().BeOfType<FileTripleStore>();
        }

        [TestCaseSource(typeof(StoresConfigurationTests), "GetProviderConfigurations")]
        public void Should_load_persistent_storage_provider(string sourceName, Type storeType)
        {
            // given
            var store = _configuration.Stores.Single(t => t.Name == sourceName);

            // when
            var virtuoso = store.CreateTripleStore();

            // then
            virtuoso.Should().BeOfType<VDS.RDF.PersistentTripleStore>();
            virtuoso.As<VDS.RDF.PersistentTripleStore>()
                    .UnderlyingStore.Should().BeOfType(storeType);
        }

        [Test]
        public void Should_load_persistent_store_declared_manually()
        {
            // given
            var store = _configuration.Stores.Single(t => t.Name == "allegro-manual");

            // when
            var virtuoso = store.CreateTripleStore();

            // then
            store.Should().BeOfType<PersistentStoreElement>();
            virtuoso.Should().BeOfType<VDS.RDF.PersistentTripleStore>();
            virtuoso.As<VDS.RDF.PersistentTripleStore>()
                    .UnderlyingStore.Should().BeOfType<VDS.RDF.Storage.AllegroGraphConnector>();
        }

        [Test]
        public void Should_load_configurations()
        {
            // given
#if NETSTANDARD16
            var loader = _configuration.OpenConfiguration("default");
#else
            var temp = Environment.CurrentDirectory;
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var loader = _configuration.OpenConfiguration("default");
            Environment.CurrentDirectory = temp;
#endif
            // then
            loader.LoadObject("store").Should().BeOfType<VDS.RDF.TripleStore>();
            loader.LoadObject(new Uri("urn:by:uri")).Should().BeOfType<VDS.RDF.TripleStore>();
        }

        public static IEnumerable GetProviderConfigurations()
        {
#if !NETSTANDARD16
            var virtuoso = typeof(VDS.RDF.Storage.VirtuosoManager);
            yield return new TestCaseData("virtuoso-connectionString", virtuoso)
                .SetDescription("Virtoso with direct connection string");
            yield return new TestCaseData("virtuoso-connectionStringName", virtuoso)
                .SetDescription("Virtoso with referenced connection string");
            yield return new TestCaseData("virtuoso-default-server", virtuoso)
                .SetDescription("Virtoso with user, pass and db");
            yield return new TestCaseData("virtuoso-default-server-timeout", virtuoso)
                .SetDescription("Virtoso with user, pass, db and timeout");
            yield return new TestCaseData("virtuoso-server", virtuoso)
                .SetDescription("Virtoso with server, port, user, pass and db");
            yield return new TestCaseData("virtuoso-server-timeout", virtuoso)
                .SetDescription("Virtoso with server, port, user, pass, db and timeout");
#endif
            var allegro = typeof(VDS.RDF.Storage.AllegroGraphConnector);
            yield return new TestCaseData("allegro-baseUri-storeID", allegro)
                .SetDescription("Allegro with two parameters");
            yield return new TestCaseData("allegro-baseUri-catalogID-storeID", allegro)
                .SetDescription("Allegro with three parameters");
            yield return new TestCaseData("allegro-baseUri-storeID-user", allegro)
                .SetDescription("Allegro with four parameters");
            yield return new TestCaseData("allegro-baseUri-catalogID-storeID-user", allegro)
                .SetDescription("Allegro with five parameters");
        }
    }
}