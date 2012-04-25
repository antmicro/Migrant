namespace AntMicro.Migrant
{
    public interface ISpeciallySerializable
    {
        void Load(PrimitiveReader reader);
        void Save(PrimitiveWriter writer);
    }
}

