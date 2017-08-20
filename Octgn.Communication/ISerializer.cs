using System;

namespace Octgn.Communication
{
    public interface ISerializer
    {
        byte[] Serialize(object o);
        object Deserialize(Type dataType, byte[] data);
    }
}
