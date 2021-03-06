﻿using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Linq;
using TwistedOak.Util;

namespace TwistedOak.Collections {
    ///<summary>Utility methods related to perishables and perishable collections.</summary>
    public static class PerishableUtilities {
        ///<summary>Feeds observed items into a new perishable collection, stopping if a given lifetime ends.</summary>
        public static PerishableCollection<T> ToPerishableCollection<T>(this IObservable<Perishable<T>> observable, Lifetime lifetime = default(Lifetime)) {
            if (observable == null) throw new ArgumentNullException(nameof(observable));
            var result = new PerishableCollection<T>();
            observable.Subscribe(result.Add, lifetime);
            return result;
        }

        /// <summary>Projects the value of each perishable element of an observable sequence into a new form.</summary>
        public static IObservable<Perishable<TOut>> LiftSelect<TIn, TOut>(this IObservable<Perishable<TIn>> observable, Func<TIn, TOut> projection) {
            if (observable == null) throw new ArgumentNullException(nameof(observable));
            if (projection == null) throw new ArgumentNullException(nameof(projection));
            return observable.Select(e => new Perishable<TOut>(projection(e.Value), e.Lifetime));
        }
        /// <summary>Filters the perishable elements of an observable sequence by value based on a predicate.</summary>
        public static IObservable<Perishable<T>> LiftWhere<T>(this IObservable<Perishable<T>> observable, Func<T, bool> predicate) {
            if (observable == null) throw new ArgumentNullException(nameof(observable));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            return observable.Where(e => predicate(e.Value));
        }
        /// <summary>Projects the value of each perishable element of a sequence into a new form.</summary>
        public static IEnumerable<Perishable<TOut>> LiftSelect<TIn, TOut>(this IEnumerable<Perishable<TIn>> sequence, Func<TIn, TOut> projection) {
            if (sequence == null) throw new ArgumentNullException(nameof(sequence));
            if (projection == null) throw new ArgumentNullException(nameof(projection));
            return sequence.Select(e => new Perishable<TOut>(projection(e.Value), e.Lifetime));
        }
        /// <summary>Filters the perishable elements of a sequence by value based on a predicate.</summary>
        public static IEnumerable<Perishable<T>> LiftWhere<T>(this IEnumerable<Perishable<T>> sequence, Func<T, bool> predicate) {
            if (sequence == null) throw new ArgumentNullException(nameof(sequence));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            return sequence.Where(e => predicate(e.Value));
        }

        /// <summary>Tracks the number of observed items that have not yet perished, counting up from 0.</summary>
        /// <param name="observable">The source observable that provides perishable items to be counted.</param>
        /// <param name="completeWhenSourceCompletes">
        /// Determines when the resulting observable completes.
        /// If true, the resulting observable completes as soon as the source observable completes.
        /// If false, the resulting observable completes when the observed count is 0 and the source observable has completed.
        /// </param>
        public static IObservable<int> ObserveNonPerishedCount<T>(this IObservable<Perishable<T>> observable, bool completeWhenSourceCompletes) {
            if (observable == null) throw new ArgumentNullException(nameof(observable));
            return new AnonymousObservable<int>(observer => {
                var exposedDisposable = new DisposableLifetime();
                var count = 0;
                var syncRoot = new object();
                var isSourceComplete = false;
                observer.OnNext(0);
                Action tryComplete = () => {
                    if (isSourceComplete && (completeWhenSourceCompletes || count == 0)) {
                        observer.OnCompleted();
                    }
                };
                observable.Subscribe(
                    item => {
                        lock (syncRoot) {
                            count += 1;
                            observer.OnNext(count);
                        }
                        item.Lifetime.WhenDead(
                            () => {
                                lock (syncRoot) {
                                    // may have finished while acquiring the lock
                                    if (exposedDisposable.Lifetime.IsDead) return;

                                    count -= 1;
                                    observer.OnNext(count);
                                    tryComplete();
                                }
                            },
                            exposedDisposable.Lifetime);
                    },
                    error => {
                        lock (syncRoot) {
                            exposedDisposable.Dispose();
                            observer.OnError(error);
                        }
                    },
                    () => {
                        lock (syncRoot) {
                            isSourceComplete = true;
                            tryComplete();
                        }
                    },
                    exposedDisposable.Lifetime);
                return exposedDisposable;
            });
        }
    }
}
