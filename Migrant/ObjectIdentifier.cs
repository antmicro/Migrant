//
// Copyright (c) 2012-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in the LICENSE file.

using System;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace Antmicro.Migrant
{
    /// <summary>
    /// Gives consecutive, unique identifiers for presented objects during its lifetime.
    /// Can also be used to retrive an object by its ID.
    /// </summary>
    /// <remarks>
    /// The first returned id is 0. For given object, if it was presented to the class
    /// earlier, the previously returned identificator is returned again. Note that the
    /// objects presented to class are remembered, so they will not be collected until
    /// the <c>ObjectIdentifier</c> lives.
    /// </remarks>
    public class ObjectIdentifier
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Migrant.ObjectIdentifier"/> class.
        /// </summary>
        public ObjectIdentifier()
        {
            objectToId = new Dictionary<object, int>();
            idToObject = new List<object>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Antmicro.Migrant.ObjectIdentifier"/> class, reusing given context.
        /// </summary>
        /// <param name="context">Context to reuse.</param>
        public ObjectIdentifier(ObjectIdentifierContext context)
        {
            objectToId = new Dictionary<object, int>();
            idToObject = context.GetObjects();
            for(var i = 0; i < idToObject.Count; i++)
            {
                var objectToAdd = idToObject[i];
                if(objectToAdd != null)
                {
                    objectToId.Add(idToObject[i], i);
                }
            }
        }

        /// <summary>
        /// Gets the context of object identifier that can be used for open stream serialization.
        /// </summary>
        public ObjectIdentifierContext GetContext()
        {
            return new ObjectIdentifierContext(idToObject);
        }

        /// <summary>
        /// For a given object, returns its unique ID. The new ID is used if object was
        /// not presented to this class earlier, otherwise the previously returned is used.
        /// </summary>
        /// <returns>
        /// The object's unique ID.
        /// </returns>
        /// <param name='o'>
        /// An object to give unique ID for.
        /// </param>
        /// <param name='isNew'>
        /// Out parameter specifying if returned id has just been generated.
        /// </param>
        public int GetId(object o, out bool isNew)
        {
            int id;
            if(objectToId.TryGetValue(o, out id))
            {
                isNew = false;
                return id;
            }

            isNew = true;
            id = idToObject.Count;
            objectToId.Add(o, id);
            idToObject.Add(o);
            return id;
        }

        /// <summary>
        /// For a given object, returns its unique ID. The new ID is used if object was
        /// not presented to this class earlier, otherwise the previously returned is used.
        /// </summary>
        /// <returns>
        /// The object's unique ID.
        /// </returns>
        /// <param name='o'>
        /// An object to give unique ID for.
        /// </param>
        public int GetId(object o)
        {
            bool fake;
            return GetId(o, out fake);
        }

        /// <summary>
        /// Sets new identifier for object.
        /// 
        /// REMARK: Setting new mapping of object to id 
        /// does not remove the old one. As a reuslt,
        /// after this operation asking for id of old
        /// object and new object results in returing
        /// the same identifier. This behaviour is intended
        /// to support surrogated objects.
        /// 
        /// </summary>
        /// <returns>The new identifier for object.</returns>
        /// <param name="o">Object</param>
        /// <param name="id">Identifier</param>
        public void SetIdentifierForObject(object o, int id)
        {
            objectToId[o] = id;
            idToObject[id] = o;
        }

        /// <summary>
        /// For an ID which was previously returned by the <see cref="Antmicro.Migrant.ObjectIdentifier.GetId(object, out bool)" /> method,
        /// returns an object for which this ID was generated.
        /// </summary>
        /// <returns>
        /// The object for which given ID was returned.
        /// </returns>
        /// <param name='id'>
        /// The unique ID, previously returned by the <see cref="Antmicro.Migrant.ObjectIdentifier.GetId(object, out bool)" /> method.
        /// </param>
        public object GetObject(int id)
        {
            if(idToObject.Count <= id || id < 0)
            {
                throw new ArgumentOutOfRangeException("id");
            }
            return idToObject[id];
        }

        /// <summary>
        /// For an ID which was previously returned by the <see cref="Antmicro.Migrant.ObjectIdentifier.GetId(object, out bool)" /> method,
        /// returns an object for which this ID was generated.
        /// </summary>
        /// <param name='id'>
        /// The unique ID, previously returned by the <see cref="Antmicro.Migrant.ObjectIdentifier.GetId(object, out bool)" /> method.
        /// </param>
        public object this[int id]
        {
            get
            {
                return GetObject(id);
            }
        }

        /// <summary>
        /// Gets the count of the unique objects presented to class. It is also
        /// the first unoccupied ID which will be returned for the new object.
        /// </summary>
        public int Count
        {
            get
            {
                return idToObject.Count;
            }
        }

        /// <summary>
        /// Clears internal elements.
        /// </summary>
        public void Clear()
        {
            idToObject.Clear();
            objectToId.Clear();
        }

        private readonly Dictionary<object, int> objectToId;
        private readonly List<object> idToObject;
    }
}

