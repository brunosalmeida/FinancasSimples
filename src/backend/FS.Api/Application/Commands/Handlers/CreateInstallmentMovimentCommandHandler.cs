namespace FS.Api.Application.Commands.Handlers
{
    using Command;
    using Domain.Core.Interfaces;
    using Domain.Core.Interfaces.Services;
    using Domain.Model;
    using Domain.Model.Validators;
    using Hangfire;
    using MediatR;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class CreateInstallmentMovimentCommandHandler : IRequestHandler<CreateInstallmentMovimentCommand, Guid>
    {
        private readonly IUserRepository _userRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionService _transactionService;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IInstallmentMovimentRepository _installmentMovimentRepository;

        public CreateInstallmentMovimentCommandHandler(IUserRepository repository, IAccountRepository accountRepository,
            ITransactionService transactionService, IBackgroundJobClient backgroundJobClient,
            IInstallmentMovimentRepository installmentMovimentRepository)
        {
            _userRepository = repository;
            _accountRepository = accountRepository;
            _transactionService = transactionService;
            _backgroundJobClient = backgroundJobClient;
            _installmentMovimentRepository = installmentMovimentRepository;
        }

        public async Task<Guid> Handle(CreateInstallmentMovimentCommand command,
            CancellationToken cancellationToken)
        {
            if (await _userRepository.Get(command.UserId) is var user && user is null)
                throw new Exception("Invalid user");

            if (await _accountRepository.Get(command.AccountId) is var account && account is null)
                throw new Exception("Invalid account");

            var installmentMoviment = new InstallmentMoviment(command.Value, command.Months, command.StartMonth, command.Description,
                command.Category, command.Type, command.AccountId,command.UserId);

            var installmentMovimentValidator = new InstallmentMovimentValidator();
            var result = await installmentMovimentValidator.ValidateAsync(installmentMoviment, default);

            if (!result.IsValid) throw new Exception(String.Join(" \n ", result.Errors));

            await _installmentMovimentRepository.Insert(installmentMoviment);
            
            var futureMoviments = SplitIntoMoviments(installmentMoviment);
            await this.ScheduleFutureMoviments(futureMoviments);
            
            return installmentMoviment.Id;
        }

        private List<Moviment> SplitIntoMoviments(InstallmentMoviment installmentMoviment)
        {
            var moviments = new List<Moviment>();
            var number = 1;
            
            for (int month = (int)installmentMoviment.StartMonth; month < installmentMoviment.EndMonth ; month++)
            {
                var description = $"({number}/{installmentMoviment.Months})-Total:{installmentMoviment.Value}-{installmentMoviment.Description}";
                var futureDate = installmentMoviment.CreatedOn.AddMonths(month);
                
                var moviment = new Moviment(installmentMoviment.InstallmentsValue, description, installmentMoviment.Category,
                    installmentMoviment.Type, installmentMoviment.AccountId, installmentMoviment.UserId);
                moviment.OverrideCreatedDate(futureDate);

                moviments.Add(moviment);

                number++;
            }
            
            return moviments;
        }

        private async Task ScheduleFutureMoviments(IList<Moviment> futureMoviments)
        {   
            foreach (var moviment in futureMoviments)
            {
                _backgroundJobClient.Schedule( () => _transactionService.CreateOrUpdateBalance(moviment),
                    new DateTimeOffset(moviment.CreatedOn).UtcDateTime);
            }

            await Task.CompletedTask;
        }
        
    }
}