namespace Arpg.Domain;

public interface IItemIdSource
{
    int ItemSequence { get; }

    string NextId();
}
