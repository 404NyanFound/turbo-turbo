using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;

using Shouldly;

using TurboTurbo.Configuration;
using TurboTurbo.Profiles.Storage;
using TurboTurbo.Profiles.Storage.Current;

using Xunit;

namespace TurboTurboTests
{
    public class ProfileMigratorTests
    {
        // shaped like a v1.0.0 profile, with non-default values for every field V2 renames or drops
        private const string V1Profile =
            @"<LocoProfile>" +
            @"<Version>1</Version><LiveryId>v1-livery</LiveryId><Enabled>true</Enabled>" +
            @"<Exhausts><LocoExhaust><Kind>Replacement</Kind><Path>ExhaustEngineSmoke(Clone)</Path>" +
            @"<Offset><x>0.1</x><y>0.2</y><z>0.3</z></Offset></LocoExhaust></Exhausts>" +
            @"<ChargerKind>Turbo</ChargerKind>" +
            @"<TurboCharger><LambdaCalibration>1.7</LambdaCalibration><RpmBoostExponent>2</RpmBoostExponent></TurboCharger>" +
            @"<Smoke><SootMaxAlpha>0.7</SootMaxAlpha><WetStackFillHeat>0.2</WetStackFillHeat>" +
            @"<WetStackReleaseHeat>0.3</WetStackReleaseHeat><WetStackFillRate>0.01</WetStackFillRate>" +
            @"<WetStackReleaseRate>0.2</WetStackReleaseRate><WetStackMistStrength>3</WetStackMistStrength>" +
            @"<WetStackMaxAlpha>0.9</WetStackMaxAlpha></Smoke>" +
            @"<SmokeEmitter><idleEmissionRate>20</idleEmissionRate><fullEmissionRate>90</fullEmissionRate>" +
            @"<lifetime>6</lifetime><startSize>0.5</startSize><sizeOverLifetimeEnd>8</sizeOverLifetimeEnd>" +
            @"<sizeOverLifetimeExponent>0.8</sizeOverLifetimeExponent><turbulenceStrength>2.5</turbulenceStrength>" +
            @"</SmokeEmitter>" +
            @"<ShimmerEmitter><idleRate>12</idleRate></ShimmerEmitter>" +
            @"<Velocity><Idle>3</Idle><FullLoad>20</FullLoad></Velocity>" +
            @"</LocoProfile>";

        [Fact]
        public void V1Profile_LoadsWithRenamedFieldsAndDefaultsForDroppedOnes()
        {
            var profile = ProfileLoader.LoadUserProfiles([XElement.Parse(V1Profile)])["v1-livery"];

            profile.Exhausts.Single().Offset.z.ShouldBe(0.3f);
            profile.TurboCharger.LambdaCalibration.ShouldBe(1.7f);
            profile.Smoke.SootMaxAlpha.ShouldBe(0.7f);
            profile.Smoke.WetStackMaxAlpha.ShouldBe(SmokeModelXml.DefaultWetStackMaxAlpha);
            profile.SmokeEmitter.startSize.ShouldBe(0.5f);
            profile.SmokeEmitter.turbulenceStrengthMax.ShouldBe(2.5f);
            profile.SmokeEmitter.turbulenceStrengthMin.ShouldBe(SmokeEmitterXml.DefaultTurbulenceStrengthMin);
            profile.SmokeEmitter.growthExponent.ShouldBe(0.8f);
            profile.SmokeEmitter.plumeSize.ShouldBe(SmokeEmitterXml.DefaultPlumeSize);
            profile.ShimmerEmitter.idleRate.ShouldBe(12f);
            profile.Velocity.ExhaustVelocityCoefficient.ShouldBe(VelocityXml.DefaultExhaustVelocityCoefficient);
        }

        [Fact]
        public void Migrate_V1_RemovesObsoleteElementsAndSetsCurrentVersion()
        {
            var migrated = ProfileMigrator.Migrate(XElement.Parse(V1Profile));

            ((int)migrated.Element("Version")).ShouldBe(ProfileSchema.CurrentVersion);
            migrated.Element("TurboCharger").Element("RpmBoostExponent").ShouldBeNull();
            migrated.Element("Smoke").Elements().Select(e => e.Name.LocalName).ShouldBe(["SootMaxAlpha"]);
            migrated.Element("SmokeEmitter").Elements().Select(e => e.Name.LocalName)
                .ShouldBe(["startSize", "growthExponent", "turbulenceStrengthMax"]);
            migrated.Element("Velocity").Elements().ShouldBeEmpty();
        }

        [Fact]
        public void Migrate_MissingVersion_TreatedAsV1()
        {
            var profile = XElement.Parse(V1Profile);
            profile.Element("Version").Remove();

            var migrated = ProfileMigrator.Migrate(profile);

            ((XElement)migrated.FirstNode).Name.LocalName.ShouldBe("Version");
            ((int)migrated.Element("Version")).ShouldBe(ProfileSchema.CurrentVersion);
            migrated.Element("SmokeEmitter").Element("turbulenceStrengthMax").ShouldNotBeNull();
        }

        [Fact]
        public void Migrate_DoesNotModifyInput()
        {
            var profile = XElement.Parse(V1Profile);

            ProfileMigrator.Migrate(profile);

            XNode.DeepEquals(profile, XElement.Parse(V1Profile)).ShouldBeTrue();
        }

        [Fact]
        public void Migrate_CurrentOrNewerVersion_ReturnsInput()
        {
            var current = LocoProfileCodec.ToElement(new LocoProfileXml { LiveryId = "current" });
            var newer = XElement.Parse(V1Profile.Replace("<Version>1</Version>", $"<Version>{ProfileSchema.CurrentVersion + 1}</Version>"));

            ProfileMigrator.Migrate(current).ShouldBeSameAs(current);
            ProfileMigrator.Migrate(newer).ShouldBeSameAs(newer);
        }

        [Fact]
        public void Settings_V1Profiles_RoundTripUnchanged()
        {
            var file = @"<?xml version=""1.0"" encoding=""utf-8""?>" +
                       @"<Settings xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">" +
                       @"<LocoProfiles>" + V1Profile + @"</LocoProfiles>" +
                       @"</Settings>";
            var serializer = new XmlSerializer(typeof(Settings));

            var settings = (Settings)serializer.Deserialize(new StringReader(file));

            XNode.DeepEquals(settings.LocoProfiles.Items.Single(), XElement.Parse(V1Profile)).ShouldBeTrue();

            using var writer = new StringWriter();
            serializer.Serialize(writer, settings);
            var written = XDocument.Parse(writer.ToString()).Root.Element("LocoProfiles");
            XNode.DeepEquals(written.Elements().Single(), XElement.Parse(V1Profile)).ShouldBeTrue();
        }

        [Fact]
        public void Settings_EmptyProfiles_Deserializes()
        {
            var serializer = new XmlSerializer(typeof(Settings));

            var settings = (Settings)serializer.Deserialize(new StringReader("<Settings><LocoProfiles /><UseSeasons>false</UseSeasons></Settings>"));

            settings.LocoProfiles.Items.ShouldBeEmpty();
            settings.UseSeasons.ShouldBeFalse();
        }
    }
}
