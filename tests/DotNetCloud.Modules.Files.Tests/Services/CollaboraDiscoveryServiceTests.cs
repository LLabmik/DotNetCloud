using DotNetCloud.Modules.Files.Data.Services;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class CollaboraDiscoveryServiceTests
{
    [TestMethod]
    public void ParseDiscoveryXml_ValidXml_ReturnsActions()
    {
        var xml = """
            <wopi-discovery>
              <net-zone name="external-http">
                <app name="writer">
                  <action name="edit" ext="docx" urlsrc="https://collabora:9980/browser/dist/cool.html?"/>
                  <action name="edit" ext="odt" urlsrc="https://collabora:9980/browser/dist/cool.html?"/>
                  <action name="view" ext="pdf" urlsrc="https://collabora:9980/browser/dist/cool.html?"/>
                </app>
                <app name="calc">
                  <action name="edit" ext="xlsx" urlsrc="https://collabora:9980/browser/dist/cool.html?"/>
                  <action name="edit" ext="ods" urlsrc="https://collabora:9980/browser/dist/cool.html?"/>
                </app>
                <app name="impress">
                  <action name="edit" ext="pptx" urlsrc="https://collabora:9980/browser/dist/cool.html?"/>
                </app>
              </net-zone>
              <proof-key modulus="abc123" exponent="def456" oldmodulus="old123" oldexponent="old456"/>
            </wopi-discovery>
            """;

        var result = CollaboraDiscoveryService.ParseDiscoveryXml(xml);

        Assert.IsTrue(result.IsAvailable);
        Assert.AreEqual(6, result.Actions.Count);
    }

    [TestMethod]
    public void ParseDiscoveryXml_EmptyNetZone_ReturnsNoActions()
    {
        var xml = """
            <wopi-discovery>
              <net-zone name="external-http">
              </net-zone>
            </wopi-discovery>
            """;

        var result = CollaboraDiscoveryService.ParseDiscoveryXml(xml);

        Assert.IsFalse(result.IsAvailable);
        Assert.AreEqual(0, result.Actions.Count);
    }

    [TestMethod]
    public void ParseDiscoveryXml_WithProofKey_ExtractsKeyValues()
    {
        var xml = """
            <wopi-discovery>
              <net-zone name="external-http">
                <app name="writer">
                  <action name="edit" ext="docx" urlsrc="https://collabora:9980/browser/dist/cool.html?"/>
                </app>
              </net-zone>
              <proof-key modulus="mod-value" exponent="exp-value" value="current-spki"
                         oldmodulus="old-mod" oldexponent="old-exp" old-value="old-spki"/>
            </wopi-discovery>
            """;

        var result = CollaboraDiscoveryService.ParseDiscoveryXml(xml);

        // ProofKey/OldProofKey carry the modulus (legacy fields)
        Assert.AreEqual("mod-value", result.ProofKey);
        Assert.AreEqual("exp-value", result.ProofKeyExponent);
        Assert.AreEqual("old-mod", result.OldProofKey);
        Assert.AreEqual("old-exp", result.OldProofKeyExponent);
        // ProofKeyValue/OldProofKeyValue carry the SubjectPublicKeyInfo for RSA verification
        Assert.AreEqual("current-spki", result.ProofKeyValue);
        Assert.AreEqual("old-spki", result.OldProofKeyValue);
    }

    [TestMethod]
    public void ParseDiscoveryXml_WithModulusProofKey_ExtractsModulus()
    {
        var xml = """
            <wopi-discovery>
              <net-zone name="external-http">
                <app name="writer">
                  <action name="edit" ext="docx" urlsrc="https://example.com/cool.html?"/>
                </app>
              </net-zone>
              <proof-key modulus="mod-value" exponent="exp-value"/>
            </wopi-discovery>
            """;

        var result = CollaboraDiscoveryService.ParseDiscoveryXml(xml);

        Assert.AreEqual("mod-value", result.ProofKey);
        Assert.AreEqual("exp-value", result.ProofKeyExponent);
    }

    [TestMethod]
    public void ParseDiscoveryXml_NoProofKey_ReturnsNulls()
    {
        var xml = """
            <wopi-discovery>
              <net-zone name="external-http">
                <app name="writer">
                  <action name="edit" ext="docx" urlsrc="https://example.com/cool.html?"/>
                </app>
              </net-zone>
            </wopi-discovery>
            """;

        var result = CollaboraDiscoveryService.ParseDiscoveryXml(xml);

        Assert.IsNull(result.ProofKey);
        Assert.IsNull(result.ProofKeyExponent);
        Assert.IsNull(result.OldProofKey);
        Assert.IsNull(result.OldProofKeyExponent);
    }

    [TestMethod]
    public void ParseDiscoveryXml_ActionWithMime_IncludesMimeType()
    {
        var xml = """
            <wopi-discovery>
              <net-zone name="external-http">
                <app name="writer">
                  <action name="edit" ext="docx" mime="application/vnd.openxmlformats-officedocument.wordprocessingml.document" urlsrc="https://example.com/cool.html?"/>
                </app>
              </net-zone>
            </wopi-discovery>
            """;

        var result = CollaboraDiscoveryService.ParseDiscoveryXml(xml);

        Assert.AreEqual(1, result.Actions.Count);
        Assert.AreEqual("application/vnd.openxmlformats-officedocument.wordprocessingml.document", result.Actions[0].MimeType);
    }

    [TestMethod]
    public void ParseDiscoveryXml_MultipleNetZones_ParsesAll()
    {
        var xml = """
            <wopi-discovery>
              <net-zone name="external-http">
                <app name="writer">
                  <action name="edit" ext="docx" urlsrc="https://example.com/cool.html?"/>
                </app>
              </net-zone>
              <net-zone name="internal-http">
                <app name="calc">
                  <action name="edit" ext="xlsx" urlsrc="https://internal.example.com/cool.html?"/>
                </app>
              </net-zone>
            </wopi-discovery>
            """;

        var result = CollaboraDiscoveryService.ParseDiscoveryXml(xml);

        Assert.AreEqual(2, result.Actions.Count);
    }

    [TestMethod]
    public void ParseDiscoveryXml_ActionMissingUrlSrc_Skipped()
    {
        var xml = """
            <wopi-discovery>
              <net-zone name="external-http">
                <app name="writer">
                  <action name="edit" ext="docx"/>
                  <action name="view" ext="pdf" urlsrc="https://example.com/cool.html?"/>
                </app>
              </net-zone>
            </wopi-discovery>
            """;

        var result = CollaboraDiscoveryService.ParseDiscoveryXml(xml);

        Assert.AreEqual(1, result.Actions.Count);
        Assert.AreEqual("pdf", result.Actions[0].Extension);
    }

    [TestMethod]
    public void ParseDiscoveryXml_CorrectAppNames()
    {
        var xml = """
            <wopi-discovery>
              <net-zone name="external-http">
                <app name="writer">
                  <action name="edit" ext="docx" urlsrc="https://example.com/cool.html?"/>
                </app>
                <app name="calc">
                  <action name="edit" ext="xlsx" urlsrc="https://example.com/cool.html?"/>
                </app>
              </net-zone>
            </wopi-discovery>
            """;

        var result = CollaboraDiscoveryService.ParseDiscoveryXml(xml);

        Assert.AreEqual("writer", result.Actions[0].AppName);
        Assert.AreEqual("calc", result.Actions[1].AppName);
    }

    [TestMethod]
    public void ParseDiscoveryXml_FetchedAtIsSet()
    {
        var xml = """
            <wopi-discovery>
              <net-zone name="external-http">
                <app name="writer">
                  <action name="edit" ext="docx" urlsrc="https://example.com/cool.html?"/>
                </app>
              </net-zone>
            </wopi-discovery>
            """;

        var before = DateTime.UtcNow;
        var result = CollaboraDiscoveryService.ParseDiscoveryXml(xml);
        var after = DateTime.UtcNow;

        Assert.IsTrue(result.FetchedAt >= before && result.FetchedAt <= after);
    }
}
