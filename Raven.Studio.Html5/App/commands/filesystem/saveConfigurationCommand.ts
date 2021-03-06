import commandBase = require("commands/commandBase");
import filesystem = require("models/filesystem/filesystem");
import configurationKey = require("models/filesystem/configurationKey");

class saveFilesystemConfigurationCommand extends commandBase {

    constructor(private fs: filesystem, private key : configurationKey, private args: any) {
        super();
    }

    execute(): JQueryPromise<any> {

        var url = "/config?name=" + encodeURIComponent(this.key.key);
        return this.put(url, JSON.stringify(this.args), this.fs)
            .done(() => this.reportSuccess("Saved configuration"))
            .fail((qXHR, textStatus, errorThrown) => this.reportError("Unable to save configuration", errorThrown));
    }
}

export = saveFilesystemConfigurationCommand; 
