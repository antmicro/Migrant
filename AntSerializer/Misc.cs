/********************************************************
 * 
 * ANT MICRO CONFIDENTIAL
 * 
 * ---
 * 
 *  (c) 2010-2012 Ant Micro <www.antmicro.com>
 *  All Rights Reserved.
 * 
 * NOTICE:  All information contained herein is, and remains
 * the property of Ant Micro. The intellectual and technical 
 * concepts contained herein are proprietary to Ant Micro and 
 * are protected by trade secret or copyright law.
 * Dissemination of this information or reproduction of this material
 * is strictly forbidden unless prior written permission is obtained
 * from Ant Micro.
 *
 */
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace AntMicro.Migrant
{    
    internal static class Misc
    {

        public static IEnumerable<FieldInfo> GetAllFields(this Type t, bool recursive = true)
        {            
            if(t == null)
            {
                return Enumerable.Empty<FieldInfo>();
            }
            if(recursive)
            {
                return t.GetFields(DefaultBindingFlags).Union(GetAllFields(t.BaseType));
            }
            return t.GetFields(DefaultBindingFlags);
        }
     
	
        private const BindingFlags DefaultBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | 
                BindingFlags.Instance | BindingFlags.DeclaredOnly;
     
  
    }
}

