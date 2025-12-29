using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpTalk.Shared.DTOs;

public class SendMessageRequest
{
    public int ChannelId { get; set; }
    public string Content { get; set; } = string.Empty;
}
