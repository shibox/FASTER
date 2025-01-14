using System;
using System.Collections.Generic;
using System.Threading;
using FASTER.core;
using NUnit.Framework;

namespace FASTER.test
{
    [TestFixture]
    internal class LogShiftTailStressTest : FasterLogTestBase
    {
        [SetUp]
        public void Setup() => base.BaseSetup();

        [TearDown]
        public void TearDown() => base.BaseTearDown();
        
        [Test]
        [Category("FasterLog")]
        public void FasterLogShiftTailStressTest()
        {
            // Get an excruciatingly slow storage device to maximize chance of clogging the flush pipeline
            device = new LocalMemoryDevice(1L << 28, 1 << 28, 2, sector_size: 512, latencyMs: 50, fileName: "stress.log");
            var logSettings = new FasterLogSettings { LogDevice = device, LogChecksum = LogChecksumType.None, LogCommitManager = manager, SegmentSizeBits = 28};
            log = new FasterLog(logSettings);

            byte[] entry = new byte[entryLength];
            for (int i = 0; i < entryLength; i++)
                entry[i] = (byte)i;
            
            for (int i = 0; i < 5 * numEntries; i++)
                log.Enqueue(entry);

            // for comparison, insert some entries without any commit records
            var referenceTailLength = log.TailAddress;

            var enqueueDone = new ManualResetEventSlim();
            var commitThreads = new List<Thread>();
            // Make sure to spin up many commit threads to expose lots of interleavings
            for (var i = 0; i < 2 * Math.Max(1, Environment.ProcessorCount - 1); i++)
            {
                commitThreads.Add(new Thread(() =>
                {
                    // Otherwise, absolutely clog the commit pipeline
                    while (!enqueueDone.IsSet)
                        log.Commit();
                }));
            }
            
            foreach (var t in commitThreads)
                t.Start();
            for (int i = 0; i < 5 * numEntries; i++)
            {
                log.Enqueue(entry);
            }
            enqueueDone.Set();

            foreach (var t in commitThreads)
                t.Join();
            
            // We expect the test to finish and not get stuck somewhere
        }
    }
}
