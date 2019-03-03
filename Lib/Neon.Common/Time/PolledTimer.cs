﻿//-----------------------------------------------------------------------------
// FILE:	    PolledTimer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Time
{
    /// <summary>
    /// Implements a timer suitable for use in scenarios that need to 
    /// poll periodically to see if an action needs to be performed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A common programming pattern is to have background threads check
    /// periodically for something to do; like resend a message, clean up
    /// idle connections, or signal a timeout.  The <see cref="PolledTimer" />
    /// class provides an easy way to encapsulate the interval and next
    /// scheduled time at which these events should happen.
    /// </para>
    /// <para>
    /// Use <see cref="PolledTimer(TimeSpan)" /> or <see cref="PolledTimer(TimeSpan,bool)" />
    /// to create a timer, specifying the firing interval.  The second constructor
    /// also specifies the <i>autoReset</i> parameter which indicates that the
    /// timer should automatically reschedule itself after being fired.  Note that
    /// one of these constructors <b>must be used</b>.  <see cref="PolledTimer" />
    /// structures created with the default constructor will thow a <see cref="InvalidOperationException" />
    /// when an attempt is made to use it.
    /// </para>
    /// <para>
    /// While being constructed, a polled timer calculates its next scheduled firing time (SYS).
    /// Note that this value generated by <see cref="SysTime" /> not a normal system time.
    /// The scheduled firing time is available as the <see cref="FireTime" /> property.
    /// Ther current interval is available as the <see cref="Interval" /> property.
    /// </para>
    /// <para>
    /// Use <see cref="HasFired" /> to determine if the timer has been fired.  This
    /// will return <c>true</c> if this is the case.  If <i>autoReset=true</i> was 
    /// passed to the constructor, then <see cref="HasFired" /> will automatically 
    /// reset the timer by scheduling the next firing time.  If <i>autoReset=false</i>,
    /// then the timer will remain in the fired state until <see cref="Reset()" /> is
    /// called.
    /// </para>
    /// <para>
    /// Asynchronous applications may find it more convienent to call <see cref="WaitAsync(TimeSpan)"/>
    /// to wait for the timer to fire.
    /// </para>
    /// <para>
    /// The <see cref="Reset()" />, <see cref="ResetImmediate" />, and <see cref="ResetRandom" /> 
    /// methods are used recalcuclate the firing time.  The first variation schedules this time as
    /// the current time plus the timer interval.  The second variation schedules
    /// the timer for immediate firing (typically used right after the timer is
    /// constructed in situations where the application wishes the timer to fire
    /// right away the first time it is polled), and the third method resets the timer
    /// to fire at a random interval between zero and the timer's interval (useful when
    /// trying to avoid having multiple timers fire at the same time).
    /// </para>
    /// <para>
    /// The <see cref="Disable()" /> method prevents the timer from firing until
    /// <see cref="Reset()" /> is called or <see cref="Interval" /> is set.
    /// This is useful for preventing a timer from firing when an operation
    /// initiated from a previous firing is still executing (perhaps on another
    /// thread).
    /// </para>
    /// </remarks>
    /// <threadsafety instance="true" />
    public sealed class PolledTimer
    {
        private const string BadTimerMsg    = "The default constructor cannot be used to create a timer.";
        private const string BadIntervalMsg = "Timer interval must be non-negative.";

        private object      syncLock  = new object();
        private TimeSpan    interval  = TimeSpan.Zero;          // The timing interval
        private DateTime    fireTime  = DateTime.MaxValue;      // Scheduled time to fire (SYS)
        private bool        autoReset = false;
        private bool        disabled  = false;

        /// <summary>
        /// The default constructor creates a timer that is initially disabled.
        /// </summary>
        public PolledTimer()
        {
            this.disabled = true;
        }

        /// <summary>
        /// The default constructor creates a timer that is initially disabled
        /// with optional auto reset capabilities. 
        /// </summary>
        /// <param name="autoReset">Indicates whether the timer should automatically reset itself after firing.</param>
        public PolledTimer(bool autoReset)
        {
            this.autoReset = autoReset;
            this.disabled  = true;
        }

        /// <summary>
        /// Constructs a timer, initializing it to fire at the specified interval.
        /// </summary>
        /// <param name="interval">The timer interval.</param>
        /// <exception cref="ArgumentException">Thrown if the interval passed is not positive.</exception>
        public PolledTimer(TimeSpan interval)
        {
            Covenant.Requires<ArgumentException>(interval >= TimeSpan.Zero);

            this.interval  = interval;
            this.fireTime  = SysTime.Now + interval;
            this.autoReset = false;
            this.disabled  = false;
        }

        /// <summary>
        /// Constructs a timer with the option of auto resetting itself.
        /// </summary>
        /// <param name="interval">The timer interval.</param>
        /// <param name="autoReset">Pass <c>true</c> to create an auto reset timer.</param>
        /// <exception cref="ArgumentException">Thrown if the interval passed is not positive.</exception>
        public PolledTimer(TimeSpan interval, bool autoReset)
            : this(interval)
        {
            Covenant.Requires<ArgumentException>(interval >= TimeSpan.Zero);

            this.autoReset = autoReset;
        }

        /// <summary>
        /// Reschedules the timer to fire at the current time plus the timer interval.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the timer was created using the default constructor.</exception>
        public void Reset()
        {
            lock (syncLock)
            {
                if (fireTime == DateTime.MinValue)
                {
                    throw new InvalidOperationException(BadTimerMsg);
                }

                this.fireTime = SysTime.Now + interval;
                this.disabled = false;
            }
        }

        /// <summary>
        /// Reschedules the timer to fire immediately.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the timer was created using the default constructor.</exception>
        public void ResetImmediate()
        {
            lock (syncLock)
            {
                if (fireTime == DateTime.MinValue)
                {
                    throw new InvalidOperationException(BadTimerMsg);
                }

                this.fireTime = SysTime.Now;
                this.disabled = false;
            }
        }

        /// <summary>
        /// Reschedules the timer to fire at a random time between now and the timer interval.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the timer was created using the default constructor.</exception>
        public void ResetRandom()
        {
            lock (syncLock)
            {
                if (fireTime == DateTime.MinValue)
                {
                    throw new InvalidOperationException(BadTimerMsg);
                }

                this.fireTime = SysTime.Now + NeonHelper.PseudoRandomTimespan(interval);
                this.disabled = false;
            }
        }

        /// <summary>
        /// Reschedules the timer to fire at a random time between the current scheduled
        /// firing time and a random interval between <see cref="TimeSpan.Zero" /> and
        /// <paramref name="interval" />.
        /// </summary>
        /// <param name="interval">The interval to be randomized (can be positive or negative).</param>
        public void ResetAddRandom(TimeSpan interval)
        {
            Covenant.Requires<ArgumentException>(interval >= TimeSpan.Zero);

            lock (syncLock)
            {
                if (fireTime == DateTime.MinValue)
                {
                    throw new InvalidOperationException(BadTimerMsg);
                }

                this.fireTime = fireTime + NeonHelper.PseudoRandomTimespan(interval);
                this.disabled = false;
            }
        }

        /// <summary>
        /// Assigns a new interval to the timer and reschedules the timer
        /// to fire at the current time plus the new interval.
        /// </summary>
        /// <param name="interval">The new timer interval.</param>
        /// <exception cref="ArgumentException">Thrown if the interval passed is not positive.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the timer was created using the default constructor.</exception>
        public void Reset(TimeSpan interval)
        {
            lock (syncLock)
            {
                if (fireTime == DateTime.MinValue)
                {
                    throw new InvalidOperationException(BadTimerMsg);
                }

                this.interval = interval;
                this.fireTime = SysTime.Now + interval;
                this.disabled = false;
            }
        }

        /// <summary>
        /// Reschedules the timer to fire at the current time plus the specified interval
        /// but <b>does not</b> change the underlying timer interval.  Subsequent timer
        /// resets will continue to use the original interval.
        /// </summary>
        /// <param name="interval">The timer wait interval.</param>
        public void ResetTemporary(TimeSpan interval)
        {
            Covenant.Requires<ArgumentException>(interval >= TimeSpan.Zero);

            lock (syncLock)
            {
                if (fireTime == DateTime.MinValue)
                {
                    throw new InvalidOperationException(BadTimerMsg);
                }

                this.fireTime = SysTime.Now + interval;
                this.disabled = false;
            }
        }

        /// <summary>
        /// Reschedules the timer to fire at the current time plus a randomly selected
        /// time between the two intervals passed.  This call <b>does not</b> change the 
        /// underlying timer interval.  Subsequent timer resets will continue to use the 
        /// original interval.
        /// </summary>
        /// <param name="minInterval">The minimum timer wait interval.</param>
        /// <param name="maxInterval">The maximum timer wait interval.</param>
        public void ResetRandomTemporary(TimeSpan minInterval, TimeSpan maxInterval)
        {
            Covenant.Requires<ArgumentException>(minInterval >= TimeSpan.Zero);
            Covenant.Requires<ArgumentException>(maxInterval >= TimeSpan.Zero);
            Covenant.Requires<ArgumentException>(minInterval <= maxInterval);

            if (minInterval == maxInterval)
            {
                ResetTemporary(minInterval);
            }
            else
            {
                ResetTemporary(minInterval + TimeSpan.FromSeconds((maxInterval - minInterval).TotalSeconds * new Random(Environment.TickCount).NextDouble()));
            }
        }

        /// <summary>
        /// Sets the timer into the fired state.
        /// </summary>
        public void FireNow()
        {
            lock (syncLock)
            {
                this.fireTime = SysTime.Now;
                this.disabled = false;
            }
        }

        /// <summary>
        /// Prevents the timer from firing until one of the <see cref="Reset()" /> methods
        /// are called or <see cref="Interval" /> is assigned a new value.
        /// </summary>
        public void Disable()
        {
            this.disabled = true;
        }

        /// <summary>
        /// Determines whether the timer has fired.
        /// </summary>
        /// <returns><c>true</c> if the timer has fired.</returns>
        /// <remarks>
        /// For auto reset timers, this property will reschedule the next
        /// firing time if the timer has fired.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the timer was created using the default constructor.</exception>
        public bool HasFired
        {
            get
            {
                lock (syncLock)
                {
                    if (disabled)
                    {
                        return false;
                    }

                    if (fireTime == DateTime.MinValue)
                    {
                        throw new InvalidOperationException(BadTimerMsg);
                    }

                    bool fired = SysTime.Now >= fireTime;

                    if (autoReset && fired)
                    {
                        Reset();
                    }

                    return fired;
                }
            }
        }

        /// <summary>
        /// Waits aynchronously for the timer to fire.
        /// </summary>
        /// <param name="pollInterval">Optional timer polling interval (defaults to <b>15 seconds</b>).</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task WaitAsync(TimeSpan pollInterval = default)
        {
            if (pollInterval <= TimeSpan.Zero)
            {
                pollInterval = TimeSpan.FromSeconds(15);
            }

            while (!HasFired)
            {
                await Task.Delay(pollInterval);
            }
        }

        /// <summary>
        /// Returns the scheduled firing time (SYS).
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the timer was created using the default constructor.</exception>
        public DateTime FireTime
        {
            get
            {
                lock (syncLock)
                {
                    if (fireTime == DateTime.MinValue)
                    {
                        throw new InvalidOperationException(BadTimerMsg);
                    }

                    return fireTime;
                }
            }
        }

        /// <summary>
        /// The current timer interval.
        /// </summary>
        /// <remarks>
        /// <note>Setting a new interval causes the timer fire time to be rescheduled.</note>
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the timer was created using the default constructor.</exception>
        /// <exception cref="ArgumentException">Thrown if the interval passed is not positive.</exception>
        public TimeSpan Interval
        {
            get
            {
                lock (syncLock)
                {
                    if (fireTime == DateTime.MinValue)
                    {
                        throw new InvalidOperationException(BadTimerMsg);
                    }

                    return interval;
                }
            }

            set
            {
                lock (syncLock)
                {
                    if (fireTime == DateTime.MinValue)
                    {
                        throw new InvalidOperationException(BadTimerMsg);
                    }

                    if (interval.Ticks < 0)
                    {
                        throw new ArgumentException(BadIntervalMsg, "value");
                    }

                    this.interval = value;
                    Reset();
                }
            }
        }
    }
}
