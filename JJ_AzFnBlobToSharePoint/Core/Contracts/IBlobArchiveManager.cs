using JJ_AzFnBlobToSharePoint.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JJ_AzFnBlobToSharePoint.Core.Contracts
{
    public interface IBlobArchiveManager
    {
        List<BlobQEntity> ErrorBlobQEntities { get;}
        bool HasErrors { get; }
        Task ArchiveEntity(List<BlobQEntity> qEntityList);
    }
}