using System;
using System.Threading;

namespace disfr.Configuration
{
    public static class ReaderWriterLockSlimExtensions
    {
        public static ReaderWriterLockSlimExtensions.ReadLock GetReadLock(
            this ReaderWriterLockSlim lockSlim)
        {
            return new ReaderWriterLockSlimExtensions.ReadLock(lockSlim);
        }

        public static ReaderWriterLockSlimExtensions.UpgradeableReadLock GetUpgradeableReadLock(
            this ReaderWriterLockSlim lockSlim)
        {
            return new ReaderWriterLockSlimExtensions.UpgradeableReadLock(lockSlim);
        }

        public static ReaderWriterLockSlimExtensions.WriteLock GetWriteLock(
            this ReaderWriterLockSlim lockSlim)
        {
            return new ReaderWriterLockSlimExtensions.WriteLock(lockSlim);
        }

        public struct ReadLock : IDisposable
        {
            private readonly ReaderWriterLockSlim gate;

            public ReadLock(ReaderWriterLockSlim gate)
            {
                this.gate = gate != null ? gate : throw new ArgumentNullException(nameof(gate));
                gate.EnterReadLock();
            }

            public void Dispose() => this.gate.ExitReadLock();
        }

        public struct UpgradeableReadLock : IDisposable
        {
            private readonly ReaderWriterLockSlim gate;

            public UpgradeableReadLock(ReaderWriterLockSlim gate)
            {
                this.gate = gate != null ? gate : throw new ArgumentNullException(nameof(gate));
                gate.EnterUpgradeableReadLock();
            }

            public void Dispose() => this.gate.ExitUpgradeableReadLock();
        }

        public struct WriteLock : IDisposable
        {
            private readonly ReaderWriterLockSlim gate;

            public WriteLock(ReaderWriterLockSlim gate)
            {
                this.gate = gate != null ? gate : throw new ArgumentNullException(nameof(gate));
                gate.EnterWriteLock();
            }

            public void Dispose() => this.gate.ExitWriteLock();
        }
    }
}
