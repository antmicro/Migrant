using System;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace AntMicro.Migrant
{
    public class ObjectIdentifier
    {
        public ObjectIdentifier()
        {
            generator = new ObjectIDGenerator();
            consecutiveIds = new Dictionary<long, int>();
            objects = new List<object>();
        }

        public int GetId(object o)
        {
            bool isNew;
            var id = generator.GetId(o, out isNew);
            if(isNew)
            {
                var localId = objects.Count;
                objects.Add(o);
                consecutiveIds.Add(id, localId);
                return localId;
            }
            return consecutiveIds[id];
        }

        public object GetObject(int id)
        {
            if(objects.Count <= id || id < 0)
            {
                throw new ArgumentOutOfRangeException("id");
            }
            return objects[id];
        }

        public int Count
        {
            get
            {
                return objects.Count;
            }
        }

        private readonly ObjectIDGenerator generator;
        private readonly Dictionary<long, int> consecutiveIds;
        private readonly List<object> objects;
    }
}

