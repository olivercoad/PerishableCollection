﻿using System;

namespace TwistedOak.Util.Soul {
    ///<summary>A soul that can be transitioned from mortal to some other phase.</summary>
    internal sealed class MortalSoul : ISoul {
        ///<summary>Callbacks to run when the lifetime is killed, immortalized, or enters limbo.</summary>
        private DoublyLinkedNode<Action> _callbacks;
        ///<summary>The current state of the lifetime.</summary>
        public Phase Phase { get; private set; }
        
        public MortalSoul() {
            this.Phase = Phase.Mortal;
        }

        /// <summary>
        /// Permanentaly transitions this lifetime to be either dead or immortal.
        /// No effect if already transitioned to the desired state.
        /// Invalid operation if already transitioned to another state.
        /// </summary>
        public void TransitionPermanently(Phase newPhase) {
            if (newPhase == Phase.Mortal) throw new ArgumentOutOfRangeException("newPhase");
            DoublyLinkedNode<Action> callbacks;
            lock (this) {
                // transition
                if (this.Phase == newPhase)
                    return;
                if (this.Phase != Phase.Mortal)
                    throw new InvalidOperationException(String.Format("Can't transition from {0} to {1}", this.Phase, newPhase));
                this.Phase = newPhase;

                // callbacks
                callbacks = this._callbacks;
                this._callbacks = null;
            }
            if (callbacks != null)
                foreach (var callback in callbacks.EnumerateOthers())
                    callback.Invoke();
        }

        /// <summary>
        /// Registers a given action to be performed when this lifetime is either immortal or dead.
        /// The returned action will remove the registration if invoked before this lifetime becomes immortal or dead.
        /// Runs the given action synchronously and returns null if this lifetime is already immortal or dead.
        /// </summary>
        public RegistrationRemover Register(Action action) {
            // hold a weak reference to the node, to ensure it can be collected after the this soul becomes non-mortal
            WeakReference weakNode;
            lock (this) {
                // safe check for already finished
                if (this.Phase != Phase.Mortal) {
                    action();
                    return Soul.EmptyRemover;
                }

                // add callback for when finished
                if (this._callbacks == null) this._callbacks = DoublyLinkedNode<Action>.CreateEmptyCycle();
                weakNode = new WeakReference(this._callbacks.Prepend(action));
            }

            // cleanup action that removes the registration
            return () => {
                var n = (DoublyLinkedNode<Action>)weakNode.Target;
                if (n != null) n.Unlink();
            };
        }
    }
}