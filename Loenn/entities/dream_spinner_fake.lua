local utils = require("utils")

local dreamSpinnerFake = {}

dreamSpinnerFake.name = "isaBag/dreamSpinFake"
dreamSpinnerFake.depth = -8500
dreamSpinnerFake.placements = {
    name = "default",
	data = {
		texturePath = "isafriend/danger/crystal"
	}
}
dreamSpinnerFake.texture = "isafriend/danger/crystal/fg_dreamspinner_fake"

function dreamSpinnerFake.selection(room, entity)
    return utils.rectangle(entity.x - 10, entity.y - 10, 21, 21)
end

return dreamSpinnerFake