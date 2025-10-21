using System;
using System.Collections.Generic;
using System.Linq;
using Y0daiiIRC.Models;

namespace Y0daiiIRC.Services
{
    public class MessageGroupingService
    {
        private readonly Dictionary<string, PendingGroup> _pendingGroups = new Dictionary<string, PendingGroup>();
        private readonly TimeSpan _groupTimeout = TimeSpan.FromSeconds(5); // Group messages within 5 seconds

        public class PendingGroup
        {
            public string GroupId { get; set; } = string.Empty;
            public string GroupTitle { get; set; } = string.Empty;
            public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
            public DateTime LastMessageTime { get; set; } = DateTime.Now;
            public MessageType GroupType { get; set; } = MessageType.System;
        }

        public bool TryAddToGroup(string groupId, string groupTitle, ChatMessage message, MessageType groupType = MessageType.System)
        {
            // Clean up expired groups
            CleanupExpiredGroups();

            if (!_pendingGroups.ContainsKey(groupId))
            {
                _pendingGroups[groupId] = new PendingGroup
                {
                    GroupId = groupId,
                    GroupTitle = groupTitle,
                    GroupType = groupType
                };
            }

            var group = _pendingGroups[groupId];
            group.Messages.Add(message);
            group.LastMessageTime = DateTime.Now;

            return true;
        }

        public ChatMessage? TryCompleteGroup(string groupId)
        {
            if (_pendingGroups.TryGetValue(groupId, out var group))
            {
                _pendingGroups.Remove(groupId);

                if (group.Messages.Count > 1)
                {
                    // Create a grouped message
                    var groupedMessage = new ChatMessage
                    {
                        Sender = "System",
                        Content = group.GroupTitle,
                        Timestamp = group.Messages.First().Timestamp,
                        SenderColor = System.Windows.Media.Colors.Gray,
                        Type = MessageType.Grouped,
                        GroupTitle = group.GroupTitle,
                        SubMessages = new List<ChatMessage>(group.Messages)
                    };

                    return groupedMessage;
                }
                else if (group.Messages.Count == 1)
                {
                    // Return the single message as-is
                    return group.Messages.First();
                }
            }

            return null;
        }

        public bool IsGroupComplete(string groupId)
        {
            if (_pendingGroups.TryGetValue(groupId, out var group))
            {
                // For whois, we expect specific numeric codes to complete the group
                if (group.GroupTitle.Contains("Whois"))
                {
                    // Check if we have the end-of-whois marker (318)
                    return group.Messages.Any(m => m.Content.Contains("End of /WHOIS list"));
                }
            }

            return false;
        }

        public void ForceCompleteGroup(string groupId)
        {
            if (_pendingGroups.ContainsKey(groupId))
            {
                _pendingGroups.Remove(groupId);
            }
        }

        private void CleanupExpiredGroups()
        {
            var expiredGroups = _pendingGroups
                .Where(kvp => DateTime.Now - kvp.Value.LastMessageTime > _groupTimeout)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var groupId in expiredGroups)
            {
                _pendingGroups.Remove(groupId);
            }
        }
    }
}
