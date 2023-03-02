using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Xml;

namespace disfr.Configuration
{
    public static class ObjectStateSaver
    {
        private static readonly ThreadSafeCache<Type, XmlSerializer> Serializers = new ThreadSafeCache<Type, XmlSerializer>((Func<Type, XmlSerializer>)(serializedType => new XmlSerializer(serializedType)));

        private static XmlSerializer CheckType(Type serializedType)
        {
            return ObjectStateSaver.Serializers[serializedType];
        }

        public static bool SaveObjectState<T>(string fileName, T savedObject)
        {
            try
            {
                using (FileStream fileStream = new FileStream(fileName, FileMode.Create))
                {
                    ObjectStateSaver.CheckType(savedObject.GetType()).Serialize((Stream)fileStream, (object)savedObject);
                }

                return true;
            }
            catch (Exception ex)
            {

                Debug.WriteLine($"Error on save object state:{ex} ");
                return false;
            }
        }

        public static bool SaveObjectState<T>(XmlWriter writer, T savedObject)
        {
            try
            {
                ObjectStateSaver.CheckType(savedObject.GetType()).Serialize(writer, (object)savedObject);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error on save object state:{ex} ");
                return false;
            }
        }

        public static bool SaveObjectState<T>(TextWriter writer, T savedObject)
        {
            try
            {
                ObjectStateSaver.CheckType(savedObject.GetType()).Serialize(writer, (object)savedObject);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error on save object state:{ex} ");
                return false;
            }
        }

        public static void SaveObjectState(StringBuilder sb, object savedObject)
        {
            foreach (PropertyInfo property in savedObject.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy))
            {
                sb.AppendFormat("{0,-30}: {1,-30}{2}", (object)property.Name, property.GetValue(savedObject, (object[])null), (object)Environment.NewLine);
            }
        }

        public static bool LoadObjectState<T>(string fileName, out T restoredObject)
        {
            return ObjectStateSaver.LoadObject<T>(fileName, typeof(T), out restoredObject) == LoadState.Normal;
        }

        public static bool LoadObjectState<T>(string fileName, Type restoredObjectType, out T restoredObject)
        {
            return ObjectStateSaver.LoadObject<T>(fileName, restoredObjectType, out restoredObject) == LoadState.Normal;
        }

        public static bool LoadObjectState<T>(XmlReader reader, Type restoredObjectType, out T restoredObject)
        {
            return ObjectStateSaver.LoadObject<T>(reader, restoredObjectType, out restoredObject) == LoadState.Normal;
        }

        public static LoadState LoadObject<T>(string fileName, out T restoredObject)
        {
            return ObjectStateSaver.LoadObject<T>(fileName, typeof(T), out restoredObject);
        }

        public static LoadState LoadObject<T>(string fileName, Type restoredObjectType, out T restoredObject)
        {
            try
            {
                return ObjectStateSaver.LoadObjectUnsafe<T>(fileName, restoredObjectType, out restoredObject);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error on load object state ['{0}']: {1} ", fileName, ex.ToString()));
                restoredObject = default(T);
                return LoadState.Corrupt;
            }
        }

        public static LoadState LoadObjectUnsafe<T>(string fileName, Type restoredObjectType, out T restoredObject)
        {
            if (!File.Exists(fileName))
            {
                restoredObject = default(T);
                return LoadState.IsNew;
            }
            XmlSerializer xmlSerializer = ObjectStateSaver.CheckType(restoredObjectType);
            using (FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                restoredObject = (T)xmlSerializer.Deserialize((Stream)fileStream);
                return LoadState.Normal;
            }
        }

        public static LoadState LoadObject<T>(XmlReader reader, Type restoredObjectType, out T restoredObject)
        {
            restoredObject = default(T);
            try
            {
                XmlSerializer xmlSerializer = ObjectStateSaver.CheckType(restoredObjectType);
                restoredObject = (T)xmlSerializer.Deserialize(reader);
                return LoadState.Normal;
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"Error on load object state:{ex} ");
                return LoadState.Corrupt;
            }
        }

        public static LoadState LoadObject<T>(TextReader reader, Type restoredObjectType, out T restoredObject) where T : class
        {
            restoredObject = default(T);
            try
            {
                XmlSerializer xmlSerializer = ObjectStateSaver.CheckType(restoredObjectType);
                restoredObject = (T)xmlSerializer.Deserialize(reader);
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"Error on load object state:{ex} ");
            }
            return (object)restoredObject != null ? LoadState.Normal : LoadState.Corrupt;
        }
    }
}
