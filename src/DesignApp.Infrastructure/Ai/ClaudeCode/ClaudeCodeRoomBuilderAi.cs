using DesignApp.Application.Interfaces;
using DesignApp.Application.Objects;
using DesignApp.Infrastructure.Ai.Claude;

using static DesignApp.Domain.Constants;
using RB = DesignApp.Infrastructure.Constants.Ai.RoomBuilder;

namespace DesignApp.Infrastructure.Ai.ClaudeCode;

/// <summary>
/// The room builder's language model on Claude Haiku through Claude Code (<c>claude -p</c>) on the developer's Claude
/// subscription, in Development only, like <see cref="ClaudeCodeInventoryProvider"/>: the same instructions and object
/// schema as <see cref="ClaudeRoomBuilderAi"/>.
/// </summary>
/// <param name="asker">Asks Claude Code.</param>
internal sealed class ClaudeCodeRoomBuilderAi(ClaudeCodeAsker asker) : IRoomBuilderAi
{
    /// <inheritdoc/>
    public async Task<Result<CatalogObject>> DescribeObjectAsync(RoomType roomType, string description, CancellationToken cancellationToken)
    {
        var message = ClaudeCodeMessage.User(ClaudeCodeMessage.Text(RB.DescribeMessage(ClaudeRoomBuilderAi.Words(roomType.ToString()), description)));
        var answer = await asker.AskAsync(RB.Model, RB.DescribeSystem, message, ClaudeRoomBuilderAi.Schema(), "described an object", cancellationToken);

        return ClaudeRoomBuilderAi.ReadObject(answer);
    }
}
