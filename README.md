# azure-eventgrid-emulator
Local development emulator for Azure Event Grid

_by Peter Barnet, Qualitas Software Ltd.  June 2021_


## Overview

Handles Azure Event Grid Events in a development/localhost context, without requiring an internet connection or an instance of Azure Event Grid.  This allows disconnected development use of Event Grid in Visual Studio (or VS Code).

#### Usage

The emulator listens for events as HTTP requests at: https://localhost:5005/api/events *

Events can be published (using package: Microsoft.Azure.EventGrid 3.2.0) as follows:

    var uri = new Uri(options.TopicUri); // set to "https://local.eventgrid.dev/api/events" in development
    var key = options.TopicKey;          // set to Guid.Empty in development
    
    var topic = new EventGridClient(new TopicCredentials(key));
    var @event = new EventGridEvent(<id>, <subject>, ...  );
    await topic.PublishEventsAsync(uri.Host, new [] { @event });  
    
Events will be pushed to Azure Function or webhook running on localhost port 7075 as follows: * 

    http://localhost:7075/runtime/webhooks/eventGrid?functionName={subscriberFunctionName}

Amend the filtering logic in EventProcessor.cs, which currently filters based on three supported EventTypes.

\* - By default, but can be adjusted in code.


## Setup Proxy & DNS Host Mapping

Add the following entry to the C:\Windows\System32\drivers\etc\hosts file:

    127.0.0.183 local.eventgrid.dev

In an elevated Command prompt, call the following command:

    netsh interface portproxy add v4tov4 listenport=443 listenaddress=127.0.0.183 connectport=5005 connectaddress=127.0.0.1


Notes-
 - Azure Function EventGrid triggers using package: Microsoft.Azure.WebJobs.Extensions.EventGrid 2.1.0)



