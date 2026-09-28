namespace System.Collections.Generic {
    /// <summary>
    /// List
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class List<T> {
        /// <summary>
        /// Value
        /// </summary>
        private T[] _value;
        /// <summary>
        /// Count
        /// </summary>
        public int Count = 0;
        /// <summary>
        /// List
        /// </summary>
        /// <param name="initsize"></param>
        public List(int initsize = 256) {
            _value = new T[initsize];
        }
        /// <summary>
        /// List
        /// </summary>
        /// <param name="t"></param>
        public List(T[] t) {
            _value = t;
        }
        /// <summary>
        /// This
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public T this[int index] {
            get {
                return _value[index];
            }
            set {
                _value[index] = value;
            }
        }

        public void Add(T t) {
            _value[Count] = t;
            Count++;
        }

        public void Insert(int index, T item, bool internalMove = false) {
            //Broken
            //if (index == IndexOf(item)) return;

            if (!internalMove)
                Count++;

            if (internalMove) {
                int _index = IndexOf(item);
                for (int i = _index; i < Count - 1; i++) {
                    _value[i] = _value[i + 1];
                }
            }

            for (int i = Count - 1; i > index; i--) {
                _value[i] = _value[i - 1];
            }
            _value[index] = item;
        }

        public T[] ToArray() {
            T[] array = new T[Count];
            for (int i = 0; i < Count; i++) {
                array[i] = this[i];
            }
            return array;
        }

        public int IndexOf(T item) {
            object boxedItem = (object)item;
            for (int i = 0; i < Count; i++) {
                object first = (object)this[i];
                if (object.ReferenceEquals(first, boxedItem) ||
                    (first != null && first.Equals(boxedItem)))
                    return i;
            }

            return -1;
        }
        public bool Remove(T item) {
            int at = IndexOf(item);

            if (at < 0)
                return false;

            RemoveAt(at);

            return true;
        }

        public void RemoveAt(int index) {
            Count--;

            for (int i = index; i < Count; i++) {
                _value[i] = _value[i + 1];
            }

            _value[Count] = default(T);
        }

        public override void Dispose() {
            _value.Dispose();
            base.Dispose();
        }

        public void Clear() {
            Count = 0;
        }
    }
}
