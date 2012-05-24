namespace AntMicro.AntSerializer
{
    public interface ISpeciallySerializable
    {
        void Load(PrimitiveReader reader);
        void Save(PrimitiveWriter writer);
    }
}

