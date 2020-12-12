﻿using FS.Data.Entities;

namespace FS.Data.Mappings
{
    internal class AccountEntityToAccountDomainMapper
    {
        public static FS.Domain.Model.Account MapFrom(Account entity)
        {
            if (entity is null) return null;

            return new FS.Domain.Model.Account
                (
                 entity.Id,
                 UserEntityToUserDomainMapper.MapFrom(entity.User),
                 MovimentEntityToMovimenteDomainMapper.MapFrom(entity.Moviments),
                 entity.CreatedOn,
                 entity.UpdatedOn.GetValueOrDefault()
                );
        }
    }
}