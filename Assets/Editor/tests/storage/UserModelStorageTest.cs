// Copyright 2020 The Blocks Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0

using com.google.apps.peltzer.client.model.export;
using com.google.apps.peltzer.client.model.storage;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;

namespace com.google.apps.peltzer.client.model.storage.tests
{
    [TestFixture]
    public class UserModelStorageTest
    {
        private string rootPath;
        private LocalUserModelStorageBackend storage;

        [SetUp]
        public void SetUp()
        {
            rootPath = Path.Combine(
              Path.GetTempPath(),
              $"OpenBlocksUserModelStorageTest-{Guid.NewGuid():N}");
            Directory.CreateDirectory(rootPath);
            storage = new LocalUserModelStorageBackend(rootPath);
        }

        [TearDown]
        public void TearDown()
        {
            string resolvedRoot = Path.GetFullPath(rootPath);
            string resolvedTemp = Path.GetFullPath(Path.GetTempPath());
            Assert.That(resolvedRoot.StartsWith(resolvedTemp, StringComparison.OrdinalIgnoreCase));
            if (Directory.Exists(resolvedRoot))
            {
                Directory.Delete(resolvedRoot, true);
            }
        }

        [Test]
        public void SaveListReadOverwriteAndDeleteUseOpaqueModelIdentity()
        {
            SaveData firstSave = CreateSaveData(1);
            ModelStorageSaveResult firstResult =
              storage.SaveModel(null, "model-id", firstSave);

            Assert.That(firstResult.Success, Is.True, firstResult.Error);
            Assert.That(firstResult.Model.Id, Is.EqualTo("model-id"));
            Assert.That(storage.ReadModelFile(firstResult.Model.Id, "model.blocks"),
              Is.EqualTo(firstSave.blocksFile));
            Assert.That(storage.ListModels().Single().Id, Is.EqualTo(firstResult.Model.Id));

            SaveData replacement = CreateSaveData(2);
            ModelStorageSaveResult replacementResult =
              storage.SaveModel(firstResult.Model.Id, "ignored-display-name", replacement);

            Assert.That(replacementResult.Success, Is.True, replacementResult.Error);
            Assert.That(replacementResult.Model.Id, Is.EqualTo(firstResult.Model.Id));
            Assert.That(storage.ReadModelFile(firstResult.Model.Id, "model.blocks"),
              Is.EqualTo(replacement.blocksFile));
            Assert.That(storage.DeleteModel(firstResult.Model.Id), Is.True);
            Assert.That(storage.ListModels(), Is.Empty);
        }

        [Test]
        public void ModelAndFileNamesRejectPathTraversal()
        {
            Assert.That(
              storage.SaveModel(null, "../outside", CreateSaveData(1)).Success,
              Is.False);
            Assert.Throws<ArgumentException>(() =>
              storage.ReadModelFile("model-id", "../outside.blocks"));
        }

        [Test]
        public void ListModelsExcludesDirectoriesWithoutRequiredModelFile()
        {
            string validDirectory = Directory.CreateDirectory(
              Path.Combine(rootPath, "valid-model")).FullName;
            File.WriteAllBytes(
              Path.Combine(validDirectory, "model.blocks"),
              new byte[] { 1 });

            Directory.CreateDirectory(Path.Combine(rootPath, "incomplete-model"));
            string unrelatedDirectory = Directory.CreateDirectory(
              Path.Combine(rootPath, "unrelated-directory")).FullName;
            File.WriteAllBytes(
              Path.Combine(unrelatedDirectory, "unrelated.blocks"),
              new byte[] { 2 });

            Assert.That(
              storage.ListModels().Select(model => model.Id),
              Is.EqualTo(new[] { "valid-model" }));
        }

        private static SaveData CreateSaveData(byte marker)
        {
            return new SaveData
            {
                blocksFile = new[] { marker, (byte)(marker + 1) },
                objFile = new[] { marker, (byte)(marker + 2) },
                mtlFile = new[] { marker, (byte)(marker + 3) },
                thumbnailBytes = new[] { marker, (byte)(marker + 4) }
            };
        }
    }
}
