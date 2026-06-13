using MDRAVA.BLL.ControlPlane.Http1;

namespace MDRAVA.BLL.ControlPlane.Caching;

public static partial class ProxyCacheEligibilityPolicy
{
    public static ProxyCacheEligibilityResult EvaluateResponseForBuffering(
        ProxyCachePolicyFacts policy,
        Http1RequestHead requestHead,
        Http1ResponseHead responseHead)
    {
        var requestEligibility = EvaluateRequest(policy, requestHead);
        if (requestEligibility is ProxyCacheEligibilityResult.RejectedResult)
        {
            return requestEligibility;
        }

        var metadataEligibility = EvaluateResponseMetadata(policy, responseHead);
        if (metadataEligibility is CacheResponseMetadataEligibility.Rejected rejectedMetadata)
        {
            return ProxyCacheEligibilityResult.Rejected(rejectedMetadata.Reason);
        }

        var framingEligibility = EvaluateResponseFraming(policy, responseHead);
        if (framingEligibility is ProxyCacheResponseFramingEligibility.Rejected rejectedFraming)
        {
            return ProxyCacheEligibilityResult.Rejected(rejectedFraming.Reason);
        }

        return ProxyCacheEligibilityResult.Accepted();
    }

    public static ProxyCacheStorageEligibilityResult EvaluateStoredResponse(
        ProxyCachePolicyFacts policy,
        Http1ResponseHead responseHead,
        long bodyLength)
    {
        var metadataEligibility = EvaluateResponseMetadata(policy, responseHead);
        if (metadataEligibility is not CacheResponseMetadataEligibility.Accepted acceptedMetadata)
        {
            return ProxyCacheStorageEligibilityResult.Rejected(
                ((CacheResponseMetadataEligibility.Rejected)metadataEligibility).Reason);
        }

        if (bodyLength > policy.MaxEntryBytes)
        {
            return ProxyCacheStorageEligibilityResult.Rejected(ReasonOversized);
        }

        return ProxyCacheStorageEligibilityResult.Accepted(acceptedMetadata.Ttl);
    }

    public static ProxyCacheResponseFramingEligibility EvaluateResponseFraming(
        ProxyCachePolicyFacts policy,
        Http1ResponseHead responseHead)
    {
        if (!policy.Enabled)
        {
            return ProxyCacheResponseFramingEligibility.Accept();
        }

        if (responseHead.Framing.Kind is Http1BodyKind.Chunked or Http1BodyKind.CloseDelimited)
        {
            return ProxyCacheResponseFramingEligibility.Reject(ReasonFraming);
        }

        if (responseHead.Framing.Kind == Http1BodyKind.ContentLength
            && responseHead.Framing.ContentLength.GetValueOrDefault() > policy.MaxEntryBytes)
        {
            return ProxyCacheResponseFramingEligibility.Reject(ReasonOversized);
        }

        return ProxyCacheResponseFramingEligibility.Accept();
    }
}
