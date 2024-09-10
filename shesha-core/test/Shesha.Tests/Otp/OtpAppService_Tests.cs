﻿using Abp.Domain.Repositories;
using Abp.Net.Mail;
using Moq;
using Shesha.Domain;
using Shesha.Domain.Enums;
using Shesha.Otp;
using Shesha.Otp.Configuration;
using Shesha.Otp.Dto;
using Shesha.Sms;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Shesha.Tests.Otp
{
    public class OtpAppService_Tests : SheshaNhTestBase
    {
        [Fact]
        public async Task SuccessOtp_Test()
        {
            var response = await CheckOtpCommon(null);

            response.IsSuccess.ShouldBe(true);
            response.ErrorMessage.ShouldBeNullOrEmpty();
        }

        [Fact]
        public async Task FailedOtp_Test()
        {
            var response = await CheckOtpCommon(v => { v.Pin += "_wrong"; });

            response.IsSuccess.ShouldBe(false);
            response.ErrorMessage.ShouldNotBeNullOrWhiteSpace();

        }

        [Fact]
        public async Task SuccessEmailLink_Test()
        {
            var response = await CheckEmailLink(null);

            response.IsSuccess.ShouldBe(true);
            response.ErrorMessage.ShouldBeNullOrEmpty();
        }

        private async Task<IVerifyPinResponse> CheckOtpCommon(Action<VerifyPinInput> transformAction)
        {
            var settings = LocalIocManager.Resolve<IOtpSettings>();

            var currentPin = string.Empty;
            var storage = new Dictionary<Guid, string>();

            var otpStorage = new Mock<IOtpStorage>();
            otpStorage.Setup(s => s.SaveAsync(It.IsAny<OtpDto>())).Returns<OtpDto>(dto =>
            {
                currentPin = dto.Pin;
                storage.Add(dto.OperationId, dto.Pin);
                return Task.CompletedTask;
            });
            otpStorage.Setup(s => s.GetAsync(It.IsAny<Guid>())).Returns<Guid>(id => Task.FromResult(new OtpDto
            {
                Pin = storage[id],
                OperationId = id,
                ExpiresOn = DateTime.MaxValue
            }));

            // Mock the required repositories and helpers
            var otpConfigRepository = new Mock<IRepository<OtpConfig, Guid>>();
            var personRepository = new Mock<IRepository<Person, Guid>>();
            var otpAppServiceHelper = new Mock<IOtpAppServiceHelper>();

            // Instantiate OtpAppService with all required parameters
            var otp = new OtpAppService(
                new NullSmsGateway(),
                LocalIocManager.Resolve<IEmailSender>(),
                otpStorage.Object,
                new OtpGenerator(settings),
                settings,
                otpConfigRepository.Object,
                personRepository.Object,
                otpAppServiceHelper.Object
            );

            var sendResponse = await otp.SendPinAsync(new SendPinInput()
            {
                Lifetime = 60,
                SendTo = "1234567890",
                SendType = OtpSendType.Sms
            });

            var verificationInput = new VerifyPinInput()
            {
                OperationId = sendResponse.OperationId,
                Pin = currentPin
            };
            transformAction?.Invoke(verificationInput);

            return await otp.VerifyPinAsync(verificationInput.OperationId, verificationInput.Pin);
        }

        private async Task<IVerifyPinResponse> CheckEmailLink(Action<VerifyPinInput> action)
        {
            var settings = LocalIocManager.Resolve<IOtpSettings>();

            var currentEmailToken = string.Empty;
            var storage = new Dictionary<Guid, string>();

            var otpStorage = new Mock<IOtpStorage>();
            otpStorage.Setup(s => s.SaveAsync(It.IsAny<OtpDto>())).Returns<OtpDto>(dto =>
            {
                currentEmailToken = dto.Pin;
                storage.Add(dto.OperationId, dto.Pin);
                return Task.CompletedTask;
            });

            otpStorage.Setup(s => s.GetAsync(It.IsAny<Guid>())).Returns<Guid>(id => Task.FromResult(new OtpDto
            {
                Pin = storage[id],
                OperationId = id,
                ExpiresOn = DateTime.MaxValue
            }));

            // Mock the required repositories and helpers
            var otpConfigRepository = new Mock<IRepository<OtpConfig, Guid>>();
            var personRepository = new Mock<IRepository<Person, Guid>>();
            var otpAppServiceHelper = new Mock<IOtpAppServiceHelper>();

            // Instantiate OtpAppService with all required parameters
            var otp = new OtpAppService(
                new NullSmsGateway(),
                LocalIocManager.Resolve<IEmailSender>(),
                otpStorage.Object,
                new OtpGenerator(settings),
                settings,
                otpConfigRepository.Object,
                personRepository.Object,
                otpAppServiceHelper.Object
            );

            var sendResponse = await otp.SendPinAsync(new SendPinInput()
            {
                Lifetime = 60,
                SendTo = "anonymous.info@boxfusion.co.za",
                SendType = OtpSendType.EmailLink
            });

            var verificationInput = new VerifyPinInput()
            {
                OperationId = sendResponse.OperationId,
                Pin = currentEmailToken
            };

            action?.Invoke(verificationInput);

            return await otp.VerifyPinAsync(verificationInput.OperationId, verificationInput.Pin);
        }
    }
}
