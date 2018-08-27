// From reference source v4.6.1/System/services/monitoring/system/diagnostics/CounterSample.cs
// And from reference source v4.6.1/.../CounterSampleCalculator.cs

//------------------------------------------------------------------------------
// <copyright file="CounterSample.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

namespace wbSystem.Diagnostics
{
    using System;

    /// <devdoc>
    ///     A struct holding the raw data for a performance counter.
    /// </devdoc>    
    public struct CounterSample
    {
        private long rawValue;
        private long baseValue;
        private long timeStamp;
        private long counterFrequency;
        private PerformanceCounterType counterType;
        private long timeStamp100nSec;
        private long systemFrequency;
        private long counterTimeStamp;

        // Dummy holder for an empty sample
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public static CounterSample Empty = new CounterSample(0, 0, 0, 0, 0, 0, PerformanceCounterType.NumberOfItems32);

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public CounterSample(long rawValue, long baseValue, long counterFrequency, long systemFrequency, long timeStamp, long timeStamp100nSec, PerformanceCounterType counterType)
        {
            this.rawValue = rawValue;
            this.baseValue = baseValue;
            this.timeStamp = timeStamp;
            this.counterFrequency = counterFrequency;
            this.counterType = counterType;
            this.timeStamp100nSec = timeStamp100nSec;
            this.systemFrequency = systemFrequency;
            this.counterTimeStamp = 0;
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public CounterSample(long rawValue, long baseValue, long counterFrequency, long systemFrequency, long timeStamp, long timeStamp100nSec, PerformanceCounterType counterType, long counterTimeStamp)
        {
            this.rawValue = rawValue;
            this.baseValue = baseValue;
            this.timeStamp = timeStamp;
            this.counterFrequency = counterFrequency;
            this.counterType = counterType;
            this.timeStamp100nSec = timeStamp100nSec;
            this.systemFrequency = systemFrequency;
            this.counterTimeStamp = counterTimeStamp;
        }

        /// <devdoc>
        ///      Raw value of the counter.
        /// </devdoc>
        public long RawValue
        {
            get
            {
                return this.rawValue;
            }
        }

        internal ulong UnsignedRawValue
        {
            get
            {
                return (ulong)this.rawValue;
            }
        }

        /// <devdoc>
        ///      Optional base raw value for the counter (only used if multiple counter based).
        /// </devdoc>
        public long BaseValue
        {
            get
            {
                return this.baseValue;
            }
        }

        /// <devdoc>
        ///      Raw system frequency
        /// </devdoc>
        public long SystemFrequency
        {
            get
            {
                return this.systemFrequency;
            }
        }

        /// <devdoc>
        ///      Raw counter frequency
        /// </devdoc>
        public long CounterFrequency
        {
            get
            {
                return this.counterFrequency;
            }
        }

        /// <devdoc>
        ///      Raw counter frequency
        /// </devdoc>
        public long CounterTimeStamp
        {
            get
            {
                return this.counterTimeStamp;
            }
        }

        /// <devdoc>
        ///      Raw timestamp
        /// </devdoc>
        public long TimeStamp
        {
            get
            {
                return this.timeStamp;
            }
        }

        /// <devdoc>
        ///      Raw high fidelity timestamp
        /// </devdoc>
        public long TimeStamp100nSec
        {
            get
            {
                return this.timeStamp100nSec;
            }
        }

        /// <devdoc>
        ///      Counter type
        /// </devdoc>
        public PerformanceCounterType CounterType
        {
            get
            {
                return this.counterType;
            }
        }

        /// <devdoc>
        ///    Static functions to calculate the performance value off the sample
        /// </devdoc>
        public static float Calculate(CounterSample counterSample)
        {
            return CounterSampleCalculator.ComputeCounterValue(counterSample);
        }

        /// <devdoc>
        ///    Static functions to calculate the performance value off the samples
        /// </devdoc>
        public static float Calculate(CounterSample counterSample, CounterSample nextCounterSample)
        {
            return CounterSampleCalculator.ComputeCounterValue(counterSample, nextCounterSample);
        }

        public override bool Equals(Object o)
        {
            return (o is CounterSample) && Equals((CounterSample)o);
        }

        public bool Equals(CounterSample sample)
        {
            return (rawValue == sample.rawValue) &&
                       (baseValue == sample.baseValue) &&
                       (timeStamp == sample.timeStamp) &&
                       (counterFrequency == sample.counterFrequency) &&
                       (counterType == sample.counterType) &&
                       (timeStamp100nSec == sample.timeStamp100nSec) &&
                       (systemFrequency == sample.systemFrequency) &&
                       (counterTimeStamp == sample.counterTimeStamp);
        }

        public override int GetHashCode()
        {
            return rawValue.GetHashCode();
        }

        public static bool operator ==(CounterSample a, CounterSample b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(CounterSample a, CounterSample b)
        {
            return !(a.Equals(b));
        }

    }
}

//------------------------------------------------------------------------------
// <copyright file="CounterSampleCalculator.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

namespace wbSystem.Diagnostics
{
    using System.Threading;
    using System;
    using System.ComponentModel;
    using wbMicrosoft.Win32;
    using System.Text;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Security.Permissions;
    using System.Globalization;
    using System.Runtime.Versioning;

    /// <devdoc>
    ///     Set of utility functions for interpreting the counter data
    ///     NOTE: most of this code was taken and ported from counters.c (PerfMon source code)
    /// </devdoc>
    public static class CounterSampleCalculator
    {
        static volatile bool perfCounterDllLoaded = false;

        /// <devdoc>
        ///    Converts 100NS elapsed time to fractional seconds
        /// </devdoc>
        /// <internalonly/>
        private static float GetElapsedTime(CounterSample oldSample, CounterSample newSample)
        {
            float eSeconds;
            float eDifference;

            if (newSample.RawValue == 0)
            {
                // no data [start time = 0] so return 0
                return 0.0f;
            }
            else
            {
                float eFreq;
                eFreq = (float)(ulong)oldSample.CounterFrequency;

                if (oldSample.UnsignedRawValue >= (ulong)newSample.CounterTimeStamp || eFreq <= 0.0f)
                    return 0.0f;

                // otherwise compute difference between current time and start time
                eDifference = (float)((ulong)newSample.CounterTimeStamp - oldSample.UnsignedRawValue);

                // convert to fractional seconds using object counter
                eSeconds = eDifference / eFreq;

                return eSeconds;
            }
        }

        /// <devdoc>
        ///    Computes the calculated value given a raw counter sample.
        /// </devdoc>
        public static float ComputeCounterValue(CounterSample newSample)
        {
            return ComputeCounterValue(CounterSample.Empty, newSample);
        }

        /// <devdoc>
        ///    Computes the calculated value given a raw counter sample.
        /// </devdoc>
        public static float ComputeCounterValue(CounterSample oldSample, CounterSample newSample)
        {
            int newCounterType = (int)newSample.CounterType;
            if (oldSample.SystemFrequency == 0)
            {
                if ((newCounterType != NativeMethods.PERF_RAW_FRACTION) &&
                    (newCounterType != NativeMethods.PERF_COUNTER_RAWCOUNT) &&
                    (newCounterType != NativeMethods.PERF_COUNTER_RAWCOUNT_HEX) &&
                    (newCounterType != NativeMethods.PERF_COUNTER_LARGE_RAWCOUNT) &&
                    (newCounterType != NativeMethods.PERF_COUNTER_LARGE_RAWCOUNT_HEX) &&
                    (newCounterType != NativeMethods.PERF_COUNTER_MULTI_BASE))
                {

                    // Since oldSample has a system frequency of 0, this means the newSample is the first sample
                    // on a two sample calculation.  Since we can't do anything with it, return 0.
                    return 0.0f;
                }
            }
            else if (oldSample.CounterType != newSample.CounterType)
            {
                throw new InvalidOperationException("Mismatched counter types");    // SR.GetString(SR.MismatchedCounterTypes));
            }

            if (newCounterType == NativeMethods.PERF_ELAPSED_TIME)
                return (float)GetElapsedTime(oldSample, newSample);

            NativeMethods.PDH_RAW_COUNTER newPdhValue = new NativeMethods.PDH_RAW_COUNTER();
            NativeMethods.PDH_RAW_COUNTER oldPdhValue = new NativeMethods.PDH_RAW_COUNTER();

            FillInValues(oldSample, newSample, oldPdhValue, newPdhValue);

            LoadPerfCounterDll();

            NativeMethods.PDH_FMT_COUNTERVALUE pdhFormattedValue = new NativeMethods.PDH_FMT_COUNTERVALUE();
            long timeBase = newSample.SystemFrequency;
            int result = SafeNativeMethods.FormatFromRawValue((uint)newCounterType, NativeMethods.PDH_FMT_DOUBLE | NativeMethods.PDH_FMT_NOSCALE | NativeMethods.PDH_FMT_NOCAP100,
                                                          ref timeBase, newPdhValue, oldPdhValue, pdhFormattedValue);

            if (result != NativeMethods.ERROR_SUCCESS)
            {
                // If the numbers go negative, just return 0.  This better matches the old behavior. 
                if (result == NativeMethods.PDH_CALC_NEGATIVE_VALUE || result == NativeMethods.PDH_CALC_NEGATIVE_DENOMINATOR || result == NativeMethods.PDH_NO_DATA)
                    return 0;
                else
                    throw new Win32Exception(result, "Performance counter PDH error"); //SR.GetString(SR.PerfCounterPdhError, result.ToString("x", CultureInfo.InvariantCulture)));
            }

            return (float)pdhFormattedValue.data;

        }


        // This method figures out which values are supposed to go into which structures so that PDH can do the 
        // calculation for us.  This was ported from Window's cutils.c
        private static void FillInValues(CounterSample oldSample, CounterSample newSample, NativeMethods.PDH_RAW_COUNTER oldPdhValue, NativeMethods.PDH_RAW_COUNTER newPdhValue)
        {
            int newCounterType = (int)newSample.CounterType;

            switch (newCounterType)
            {
                case NativeMethods.PERF_COUNTER_COUNTER:
                case NativeMethods.PERF_COUNTER_QUEUELEN_TYPE:
                case NativeMethods.PERF_SAMPLE_COUNTER:
                case NativeMethods.PERF_OBJ_TIME_TIMER:
                case NativeMethods.PERF_COUNTER_OBJ_TIME_QUEUELEN_TYPE:
                    newPdhValue.FirstValue = newSample.RawValue;
                    newPdhValue.SecondValue = newSample.TimeStamp;

                    oldPdhValue.FirstValue = oldSample.RawValue;
                    oldPdhValue.SecondValue = oldSample.TimeStamp;
                    break;

                case NativeMethods.PERF_COUNTER_100NS_QUEUELEN_TYPE:
                    newPdhValue.FirstValue = newSample.RawValue;
                    newPdhValue.SecondValue = newSample.TimeStamp100nSec;

                    oldPdhValue.FirstValue = oldSample.RawValue;
                    oldPdhValue.SecondValue = oldSample.TimeStamp100nSec;
                    break;

                case NativeMethods.PERF_COUNTER_TIMER:
                case NativeMethods.PERF_COUNTER_TIMER_INV:
                case NativeMethods.PERF_COUNTER_BULK_COUNT:
                case NativeMethods.PERF_COUNTER_LARGE_QUEUELEN_TYPE:
                case NativeMethods.PERF_COUNTER_MULTI_TIMER:
                case NativeMethods.PERF_COUNTER_MULTI_TIMER_INV:
                    newPdhValue.FirstValue = newSample.RawValue;
                    newPdhValue.SecondValue = newSample.TimeStamp;

                    oldPdhValue.FirstValue = oldSample.RawValue;
                    oldPdhValue.SecondValue = oldSample.TimeStamp;
                    if (newCounterType == NativeMethods.PERF_COUNTER_MULTI_TIMER || newCounterType == NativeMethods.PERF_COUNTER_MULTI_TIMER_INV)
                    {
                        //  this is to make PDH work like PERFMON for
                        //  this counter type
                        newPdhValue.FirstValue *= (uint)newSample.CounterFrequency;
                        if (oldSample.CounterFrequency != 0)
                        {
                            oldPdhValue.FirstValue *= (uint)oldSample.CounterFrequency;
                        }
                    }

                    if ((newCounterType & NativeMethods.PERF_MULTI_COUNTER) == NativeMethods.PERF_MULTI_COUNTER)
                    {
                        newPdhValue.MultiCount = (int)newSample.BaseValue;
                        oldPdhValue.MultiCount = (int)oldSample.BaseValue;
                    }


                    break;
                //
                //  These counters do not use any time reference
                //
                case NativeMethods.PERF_COUNTER_RAWCOUNT:
                case NativeMethods.PERF_COUNTER_RAWCOUNT_HEX:
                case NativeMethods.PERF_COUNTER_DELTA:
                case NativeMethods.PERF_COUNTER_LARGE_RAWCOUNT:
                case NativeMethods.PERF_COUNTER_LARGE_RAWCOUNT_HEX:
                case NativeMethods.PERF_COUNTER_LARGE_DELTA:
                    newPdhValue.FirstValue = newSample.RawValue;
                    newPdhValue.SecondValue = 0;

                    oldPdhValue.FirstValue = oldSample.RawValue;
                    oldPdhValue.SecondValue = 0;
                    break;
                //
                //  These counters use the 100 Ns time base in thier calculation
                //
                case NativeMethods.PERF_100NSEC_TIMER:
                case NativeMethods.PERF_100NSEC_TIMER_INV:
                case NativeMethods.PERF_100NSEC_MULTI_TIMER:
                case NativeMethods.PERF_100NSEC_MULTI_TIMER_INV:
                    newPdhValue.FirstValue = newSample.RawValue;
                    newPdhValue.SecondValue = newSample.TimeStamp100nSec;

                    oldPdhValue.FirstValue = oldSample.RawValue;
                    oldPdhValue.SecondValue = oldSample.TimeStamp100nSec;
                    if ((newCounterType & NativeMethods.PERF_MULTI_COUNTER) == NativeMethods.PERF_MULTI_COUNTER)
                    {
                        newPdhValue.MultiCount = (int)newSample.BaseValue;
                        oldPdhValue.MultiCount = (int)oldSample.BaseValue;
                    }
                    break;
                //
                //  These counters use two data points
                //
                case NativeMethods.PERF_SAMPLE_FRACTION:
                case NativeMethods.PERF_RAW_FRACTION:
                case NativeMethods.PERF_LARGE_RAW_FRACTION:
                case NativeMethods.PERF_PRECISION_SYSTEM_TIMER:
                case NativeMethods.PERF_PRECISION_100NS_TIMER:
                case NativeMethods.PERF_PRECISION_OBJECT_TIMER:
                case NativeMethods.PERF_AVERAGE_TIMER:
                case NativeMethods.PERF_AVERAGE_BULK:
                    newPdhValue.FirstValue = newSample.RawValue;
                    newPdhValue.SecondValue = newSample.BaseValue;

                    oldPdhValue.FirstValue = oldSample.RawValue;
                    oldPdhValue.SecondValue = oldSample.BaseValue;
                    break;

                default:
                    // an unidentified counter was returned so
                    newPdhValue.FirstValue = 0;
                    newPdhValue.SecondValue = 0;

                    oldPdhValue.FirstValue = 0;
                    oldPdhValue.SecondValue = 0;
                    break;
            }
        }

        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        private static void LoadPerfCounterDll()
        {
            if (perfCounterDllLoaded)
                return;

            new FileIOPermission(PermissionState.Unrestricted).Assert();

            string installPath = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
            string perfcounterPath = Path.Combine(installPath, "perfcounter.dll");
            if (SafeNativeMethods.LoadLibrary(perfcounterPath) == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            perfCounterDllLoaded = true;
        }
    }
}


