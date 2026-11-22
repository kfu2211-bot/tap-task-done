namespace tts.Data;

public record FreelancerTaskActions(MarketplaceTask Task, MarketplaceOffer Offer, string View);
public record ClientTaskActions(MarketplaceTask Task, bool HasAccepted, bool CanBookAgain);
