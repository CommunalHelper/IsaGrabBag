local drawableSprite = require("structs.drawable_sprite")
local colors = require("consts.xna_colors")

local dreamSpinner = {}

dreamSpinner.name = "isaBag/dreamSpinner"
dreamSpinner.depth = -8500
dreamSpinner.placements = {
    {
        name = "default",
        data = {
			texturePath = "isafriend/danger/crystal",
            useOnce = false
        }
    },
    {
        name = "oneUse",
        data = {
			texturePath = "isafriend/danger/crystal",
            useOnce = true
        }
    }
}

local texture = "isafriend/danger/crystal/fg_dreamspinner"

function dreamSpinner.sprite(room, entity)
    local sprite = drawableSprite.fromTexture(texture, entity)
    
    if entity.useOnce then
        sprite:setColor(colors.Orange)
    end
    
    return sprite
end

function dreamSpinner.selection(room, entity)
    return utils.rectangle(entity.x - 10, entity.y - 10, 21, 21)
end

return dreamSpinner