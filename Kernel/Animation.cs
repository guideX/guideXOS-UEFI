using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System;
using System.Collections.Generic;

namespace guideXOS {
    /// <summary>
    /// Represents an animation that changes a value over time based on a period and increment.
    /// </summary>
    public class Animation {
        /// <summary>
        /// Gets or sets the current value of the animation.
        /// </summary>
        public int Value;
        
        /// <summary>
        /// Gets or sets the minimum value the animation can reach.
        /// </summary>
        public int MinimumValue;
        
        /// <summary>
        /// Gets or sets the maximum value the animation can reach.
        /// </summary>
        public int MaximumValue;

        /// <summary>
        /// Gets or sets the period in milliseconds for each value change.
        /// </summary>
        public int PeriodInMS;
        
        /// <summary>
        /// Gets or sets the amount the value changes during each period.
        /// </summary>
        public int ValueChangesInPeriod;

        /// <summary>
        /// Gets or sets whether the animation is stopped.
        /// </summary>
        public bool Stopped;

        /// <summary>
        /// Initializes a new instance of the <see cref="Animation"/> class with default values.
        /// </summary>
        public Animation() {
            PeriodInMS = 1;
            ValueChangesInPeriod = 1;
        }
    }

    /// <summary>
    /// Manages and updates all active animations in the system.
    /// </summary>
    public static class Animator {
        static List<Animation> Animations;

        private static void EnsureCollection() {
            if (Animations == null) {
                Animations = new List<Animation>();
            }
        }

        /// <summary>
        /// Initializes the animator system and sets up interrupt handling for animation updates.
        /// </summary>
        public static unsafe void Initialize() {
            Animations = new List<Animation>();
            Interrupts.EnableInterrupt(0x20, &OnInterrupt);
        }

        /// <summary>
        /// Adds an animation to the list of managed animations.
        /// </summary>
        /// <param name="ani">The animation to add.</param>
        public static void AddAnimation(Animation ani) {
            EnsureCollection();
            Animations.Add(ani);
        }

        /// <summary>
        /// Removes and disposes an animation from the managed list.
        /// </summary>
        /// <param name="ani">The animation to dispose.</param>
        public static void DisposeAnimation(Animation ani) {
            if (Animations == null) return;
            Animations.Remove(ani);
            ani.Dispose();
        }

        /// <summary>
        /// Interrupt handler that updates all active animations based on the timer tick.
        /// </summary>
        public static void OnInterrupt() {
            for (int i = 0; i < Animations.Count; i++) {
                Animation v = Animations[i];
                if (!v.Stopped) {
                    if (v.PeriodInMS == 0) continue;
                    if ((Timer.Ticks % (ulong)v.PeriodInMS) == 0) {
                        v.Value = Math.Clamp(v.Value + v.ValueChangesInPeriod, v.MinimumValue, v.MaximumValue);
                    }
                }
            }
        }
    }
}
